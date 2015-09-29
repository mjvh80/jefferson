/*   
   Copyright 2014 Marcus van Houdt

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */
using Jefferson.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using BinOp = System.Func<System.Linq.Expressions.Expression, System.Linq.Expressions.Expression, System.Linq.Expressions.Expression>;

namespace Jefferson
{
   using BinConversion = Func<BinOp, BinOp>;
   using BinOpMap = Tuple<String[], BinOp>;
   using Production = Func<Expression>;

   // Temporary expression representing an unresolved identifier.
   internal class _IdentifierExpression : Expression
   {
      public String Identifier;
   }

   [Flags]
   public enum ExpressionParsingFlags
   {
      None = 0,

      /// <summary>
      /// Generates symbols that may aid in debugging.
      /// </summary>
      AddPdbGenerator = 1 << 0,

      /// <summary>
      /// Ignore case in things like string matching or name resolving.
      /// </summary>
      IgnoreCase = 1 << 1,

      /// <summary>
      /// Use the current culture for things like ToString.
      /// Note that numbers are always parsed in the invariant culture.
      /// </summary>
      UseCurrentCulture = 1 << 2,

      /// <summary>
      /// If the expression is empty or whitespace or comments, treat it as the empty string.
      /// </summary>
      EmptyExpressionIsEmptyString = 1 << 3
   }

   public class CompiledExpression<TContext, TOutput>
   {
      public Expression<Func<TContext, TOutput>> Ast;
      public Type OutputType; // invariant: typeof(TOutput).IsAssignableFrom(OutputType)
   }

   public class PredicateParser<TContext> : ExpressionParser<TContext, Boolean> { }

   internal delegate BinOpMap DefineOpFn(BinOp f, params String[] operators);
   internal delegate Production MakeBinOpProductionFn(Func<Production> leftExpr, params BinOpMap[] ops);
   internal delegate void Throw(String msg, params Object[] args);

   public delegate Expression NameResolverDelegate(Expression thisExpr, String name, String typeName, NameResolverDelegate defaultResolver);

   internal static class _GenericHelpers
   {
      public static Expression CallInline(this Expression e, String method)
      {
         var f = (LambdaExpression)typeof(_GenericHelpers).GetMethod(method).MakeGenericMethod(e.Type).Invoke(null, null);

         return _ReplaceParameterVisitor.ReplaceParamWith(f.Parameters[0], e, f.Body);
      }

      public static Expression<Func<T, Boolean>> IsZero<T>()
      {
         return t => t.Equals(default(T));
      }
   }

   internal class _ReplaceParameterVisitor : ExpressionVisitor
   {
      private Expression _mRepl;
      private ParameterExpression _mParamExpr;
      private _ReplaceParameterVisitor() { }
      public static Expression ReplaceParamWith(ParameterExpression @param, Expression p, Expression body)
      {
         return new _ReplaceParameterVisitor { _mParamExpr = @param, _mRepl = p }.Visit(body);
      }
      protected override Expression VisitParameter(ParameterExpression node)
      {
         // Simply replace with the given expression
         if (node == _mParamExpr) return _mRepl;
         return base.VisitParameter(node);
      }
   }

   [DebuggerDisplay("{Target.ToString()}")]
   public delegate TOutput ExpressionDelegate<in TContext, out TOutput>(TContext context);

   public class ExpressionParser<TContext, TOutput>
   {
      // For debugger purposes only.
      private String _ContextTypeString { get { return typeof(TContext).Name; } }
      private String _OutputTypeString { get { return typeof(TOutput).Name; } }

      public ExpressionDelegate<TContext, TOutput> ParseExpression(String expr)
      {
         return ParseExpression(expr, null);
      }

      /// <summary>
      /// Convenience overload to obtain actual type from the given object instance. This is mainly useful for use with anonymous types.
      /// </summary>
      public ExpressionDelegate<TContext, TOutput> ParseExpression(String expr, Object instanceOfActualType)
      {
         if (instanceOfActualType == null)
            throw new ArgumentNullException("instanceOfActualType");

         return ParseExpression(expr, instanceOfActualType.GetType());
      }

      /// <summary>
      /// Convenience overload for actual type.
      /// </summary>
      public ExpressionDelegate<TContext, TOutput> ParseExpression<TDerivedContext>(String expr) where TDerivedContext : TContext
      {
         return ParseExpression(expr, typeof(TDerivedContext));
      }

      /// <summary>
      /// The actualContextType should be a type derived from TContext. This is particularly useful if using anonymous types derived from Object.
      /// </summary>
      public ExpressionDelegate<TContext, TOutput> ParseExpression(String expr, Type actualContextType)
      {
         return ParseExpression(expr, null, ExpressionParsingFlags.None, actualContextType);
      }

      public ExpressionDelegate<TContext, TOutput> ParseExpression(String expr, NameResolverDelegate nameResolver = null, ExpressionParsingFlags flags = ExpressionParsingFlags.None, Type actualContextType = null)
      {
         try
         {
            var compileResult = _ParseExpressionInternal(expr, nameResolver, flags, actualContextType);
            var ast = compileResult.Ast;

            // Reduce if possible.
            while (ast.CanReduce) ast.ReduceAndCheck();

            Func<TContext, TOutput> compiledResult;
            if (flags.HasFlag(ExpressionParsingFlags.AddPdbGenerator))
               compiledResult = ast.Compile(DebugInfoGenerator.CreatePdbGenerator());
            else
               compiledResult = ast.Compile();

            // Bind an instance of _ExpressionDebugContext to the resulting Func so that we may use 'this' for debug display.
            return new _ExpressionDebugContext(expr, compiledResult).RunFunc;
         }
         catch (SyntaxException)
         {
            throw;
         }
         catch (Exception e)
         {
            throw SyntaxException.Create(e, "Parse or Compile error for expression '{0}'", expr);
         }
      }

      public Boolean TryParseExpression(String expr, out ExpressionDelegate<TContext, TOutput> expression, NameResolverDelegate nameResolver = null, ExpressionParsingFlags flags = ExpressionParsingFlags.None, Type actualContextType = null)
      {
         expression = null;

         // For now, we'll do this using exceptions. Maybe at some point we can optimize a bit.
         try
         {
            expression = ParseExpression(expr, nameResolver, flags, actualContextType);
            return true;
         }
         catch
         {
            return false;
         }
      }

      /// <summary>
      /// This is useful to allow for statements in the form of delegates. For example see usage.
      /// </summary>
      private static Expression<Func<TInput, TOutput2>> _WrapExprInline<TInput, TOutput2>(Expression<Func<TInput, TOutput2>> input, Expression<Func<Func<TInput, TOutput2>, Func<TInput, TOutput2>>> expr)
      {
         var p = Expression.Parameter(typeof(TInput));
         return Expression.Lambda<Func<TInput, TOutput2>>(Expression.Invoke(_ReplaceParameterVisitor.ReplaceParamWith(expr.Parameters[0], input, expr.Body), p), p);

         // Not inline:
         // return Expression.Lambda<Func<TInput, TOutput2>>(Expression.Invoke(Expression.Invoke(expr, input), p), p);
      }

      /// <summary>
      /// Allows the expression on the right being written as a lambda (compiled to an expression by the c# compiler).
      /// The parameter of this lambda is replaced with the given input.
      /// </summary>
      private static Expression _GetExpr<TInput, TOutput2>(Expression input, Expression<Func<TInput, TOutput2>> repl, Boolean inline = true)
      {
         if (inline)
            return _ReplaceParameterVisitor.ReplaceParamWith(repl.Parameters[0], input, repl.Body);
         else
            return Expression.Invoke(repl, input);
      }

      internal static NameResolverDelegate _GetDefaultNameResolver(ExpressionParsingFlags flags = ExpressionParsingFlags.None)
      {
         return (thisExpr, name, typeName, @default) =>
         {
            var caseFlags = (flags & ExpressionParsingFlags.IgnoreCase) != 0 ? BindingFlags.IgnoreCase : 0;

            // Check for an instance property or field.
            var binding = BindingFlags.Public | BindingFlags.Instance | caseFlags;
            if (typeName == null && thisExpr.Type.GetField(name, binding) != null)
               return Expression.Field(thisExpr, thisExpr.Type.GetField(name, binding));

            if (typeName == null && thisExpr.Type.GetProperty(name, binding) != null)
               return Expression.Property(thisExpr, thisExpr.Type.GetProperty(name, binding));

            // Update binding for static fields.
            binding = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy | caseFlags;
            if (typeName == null && (thisExpr.Type.GetField(name, binding) != null))
               return Expression.Field(null, thisExpr.Type.GetField(name, binding));

            if (typeName == null && thisExpr.Type.GetProperty(name, binding) != null)
               return Expression.Property(null, thisExpr.Type.GetProperty(name, binding));

            if (typeName == null) return null;

            var type = Utils.FindTypeInAppDomain(typeName, except: false);
            if (type == null) return null;

            if (type.GetField(name, binding) != null)
               return Expression.Field(null, type.GetField(name, binding));
            if (type.GetProperty(name, binding) != null)
               return Expression.Property(null, type.GetProperty(name, binding));

            return null;
         };
      }

      // Parses expressions.
      // operators are: >, >=, <=, ==, !=, && and ||              
      // Allows "and" instead of &&.
      // BNF:
      // expression := condExpr                                           
      // condExpr := nullExpr [ '?' expr ':' expr ]
      // nullExpr := orExpr [ '??' nullExpr ]
      // orExpr := andExpr [ '||' andExpr ]
      // andExpr := equalExpr [ '&&' equalExpr ]
      // equalExpr := relExpr [ (('==' | '!=' ) relExpr) |
      //                       (('=~' | '!~')  relExpr>regex) ]
      // relExpr := addExpr [ ('<' | '<=' | '>' | '>=' | '≤' | '≥') addExpr ]
      // addExpr := multExpr [ ('+' | '-') multExpr ]
      // multExpr := unaryExpr [ ('*' | '/') unaryExpr ]
      // unaryExpr := [ '!' | '-' | '+' | '~' ] primaryExpr
      // primaryExpr := unitExpr [ ('.' unitExpr)+ |
      //                          ('[' indexExpr ']') |
      //                          ('(' invocExpr ')') ]
      // unitExpr := ( '(' expr ')' ) |
      //             ( '"' stringExpr '"') |
      //             ( '\'' stringExpr '\'') |
      //             ( regexExpr ) |
      //             ( numberExpr ) |
      //             ( true ) | ( false ) | ( null ) |
      //             ( identifierExpr )
      internal CompiledExpression<TContext, TOutput> _ParseExpressionInternal(String expr, NameResolverDelegate nameResolver = null, ExpressionParsingFlags flags = ExpressionParsingFlags.None, Type actualContextType = null, Func<String, Object, Object> valueFilter = null)
      {
         #region Parser

         if (String.IsNullOrEmpty(expr) && (flags & ExpressionParsingFlags.EmptyExpressionIsEmptyString) == 0)
            throw SyntaxException.Create("null or empty expression");

         if (actualContextType == null)
            actualContextType = typeof(TContext);

         if (!typeof(TContext).IsAssignableFrom(actualContextType))
            throw Utils.InvalidOperation("Actual context type '{0}' is not of type '{1}'.", actualContextType.FullName, typeof(TContext).FullName);

         // Pre-declare those so they can be closed over.
         Func<String> nameToken = null;
         Func<Expression> expression = null, equalExpr = null, relExpr = null, multExpr = null, addExpr = null, unitExpr = null, unaryExpr = null, primaryExpr = null, condExpr = null, nullExpr = null, orExpr = null, andExpr = null, binAndExpr = null, binOrExpr = null, binExOrExpr = null, ifExpr = null;

         Int32 i = 0;


         var defaultResolver = _GetDefaultNameResolver(flags);
         if (nameResolver == null)  // Default: get a property or field by the given name.
            nameResolver = defaultResolver;

         var contextExpr = Expression.Parameter(typeof(TContext), "context");
         var actualContextExpr = Expression.Convert(contextExpr, actualContextType); // just cast to the derived context type

         if (valueFilter != null)
         {
            // Wrap the resolver using the filter.
            var currentResolver = nameResolver;
            nameResolver = (Expression thisExpr, String name, String typeName, NameResolverDelegate @base) =>
            {
               var result = currentResolver(thisExpr, name, typeName, @base);
               if (result == null) return null;

               var fullName = typeName == null ? name : typeName + "." + name;
               var type = result.Type;

               // Call the custom hook, but ensure that we don't lose our type.
               return Expression.Convert(Expression.Invoke(Expression.Constant(valueFilter), Expression.Constant(fullName), Expression.Convert(result, typeof(Object))),
                                         type);
            };
         }

         #region Parsing Utilities

         Func<Boolean> skipWhitespace = () => { var j = i; for (; i < expr.Length && Char.IsWhiteSpace(expr[i]); i += 1) ; return i > j; };
         Func<Boolean> skipComments = () =>
         {
            if (i < expr.Length - 1 && expr[i] == '/' && expr[i + 1] == '/')
            {
               if (i == expr.Length - 2) i = expr.Length;
               else
               {
                  i = Math.Min(expr.IndexOf('\n', i + 2), expr.IndexOf('\r', i + 2));
                  if (i < 0) i = expr.Length;
                  else i += 1;
               }
               return true;
            }
            return false;
         };

         Action advanceWhitespace = () => { while (skipWhitespace() || skipComments()) { } };

         Func<String, Boolean> advanceIfMatch = (s) =>
         {
            var r = new Regex("\\G(" + s + ")", RegexOptions.CultureInvariant);
            var m = r.Match(expr, startat: i);
            if (!m.Success) return false;
            i += m.Length;
            advanceWhitespace();
            return true;
         };
         Func<String, String> optAdvance = s => { var j = i; if (advanceIfMatch(s)) return expr.Substring(j, i - j); return null; };
         Func<Func<Boolean>, String> advanceWhile = c => { var token = ""; for (; i < expr.Length && c(); i++) { token += expr[i]; } return token; };
         Throw throwExpected = (s, args) => { throw SyntaxException.Create(expr, i, "Expected {0}", String.Format(s, args)); };
         Action<String> expectAdvance = (s) => { if (!advanceIfMatch(s)) throwExpected(s); };

         // Defines a mapping of operator strings (e.g. +) to binary expression.
         DefineOpFn op = (e, ops) => Tuple.Create(ops, e);

         MakeBinOpProductionFn binaryOperator = (leftExpr, opDefs) => () =>
         {
            var result = leftExpr()(); // this is just to close over stuff

            advanceWhitespace();

            // Find the first operator match.
         next: foreach (var def in opDefs.Where(def => def.Item1.Any(opStr => advanceIfMatch(opStr))))
            {
               result = def.Item2(result, leftExpr()()); // fnWidenTypes(result, leftExpr()(), def.Item2);
               advanceWhitespace();
               goto next;
            }

            return result;
         };

         Func<Expression, Expression> enumToNum = e =>
         {
            if (!e.Type.IsEnum) return e;
            return Expression.Convert(e, Enum.GetUnderlyingType(e.Type));
         };

         #endregion

         var _false = Expression.Constant(false);
         var _true = Expression.Constant(true);
         var _zero = Expression.Constant(0);
         var _nullStr = Expression.Constant(null, typeof(String));
         var _null = Expression.Constant(null);

         #region Conversions

         // Converts the given expression to a bool value, using:
         // - non zero values are true
         // - non empty strings are true
         Func<Expression, Expression> convertToBool = e =>
         {
            if (e._is_<Boolean>()) return e;
            if (e.IsNullConstant()) return _false; // shortcut

            if (e.Type.IsValueType)
               return Expression.Not(e.CallInline("IsZero"));

            // Strings.
            if (e._is_<String>()) return _GetExpr<String, Boolean>(e, s => s != null && s.Length != 0, inline: false);

            // Any reference type, false if null, true otherwise. Could consider collections, not sure. In js !![] === true.
            return e.IfTypeIs<Boolean>(e.DynCast<Boolean>(), Expression.Not(Expression.Equal(e, _null)));
         };
         // "foo" == true
         BinConversion widenIfOneBool = f => (e1, e2) =>
         {
            if (e1._is_<Boolean>() || e2._is_<Boolean>())
               return f(convertToBool(e1), convertToBool(e2));
            if (e1.Type.IsValueType && e2.Type.IsValueType)
               return f(e1, e2);
            // At runtime, see if we have boxed bools.
            return e1.IfTypeIs<Boolean>(f(e1.DynCast<Boolean>(), convertToBool(e2)),
                     e2.IfTypeIs<Boolean>(f(convertToBool(e1), e2.DynCast<Boolean>()),
                        f(e1, e2)));
         };

         BinConversion convEnums = f => (e1, e2) =>
         {
            if (e1.Type.IsEnum && e2.Type.IsEnum) return f(e1, e2);   // todo < what if different types
            if (!e1.Type.IsEnum && !e2.Type.IsEnum) return f(e1, e2);

            var ignoreCase = flags.HasFlag(ExpressionParsingFlags.IgnoreCase);

            if (e1.Type == typeof(String))
            {
               e1 = Expression.Call(Utils.GetMethod(() => Enum.Parse(null, null, false)), Expression.Constant(e2.Type), e1, Expression.Constant(ignoreCase));
               e1 = Expression.Convert(e1, e2.Type);
            }
            else if (e2.Type == typeof(String))
            {
               e2 = Expression.Call(Utils.GetMethod(() => Enum.Parse(null, null, false)), Expression.Constant(e1.Type), e2, Expression.Constant(ignoreCase));
               e2 = Expression.Convert(e2, e1.Type);
            }

            // todo: integral conversions

            return f(e1, e2);
         };

         BinConversion widenNums = f => (e1, e2) =>
         {
            if (e1.Type.IsEnum) e1 = enumToNum(e1);
            if (e2.Type.IsEnum) e2 = enumToNum(e2);

            if (e1.Type == e2.Type) return f(e1, e2);

            // One is not a value type thus not a number. Note we won't attempt to cast etc.
            if (!e1.IsNumeric() || !e2.IsNumeric()) return f(e1, e2);

            if (e1.IsIntegral() && e2.IsIntegral())
            {
               var d = Marshal.SizeOf(e1.Type) - Marshal.SizeOf(e2.Type);
               if (d < 0) // e2 > e1
                  return f(Expression.Convert(e1, e2.Type), e2);
               else if (d > 0) // e1 > e2
                  return f(e1, Expression.Convert(e2, e1.Type));

               // Types are of the same size, but one is signed and the other isn't.
               switch (Marshal.SizeOf(e1.Type))
               {
                  // As C# does, lift to int.
                  case 1:
                  case 2:
                     return f(Expression.Convert(e1, typeof(Int32)), Expression.Convert(e2, typeof(Int32)));
                  case 4:
                  case 8: // case not supported by C#, but we'll simply convert to signed
                     return f(Expression.Convert(e1, typeof(Int64)), Expression.Convert(e2, typeof(Int64)));

                  default: throw new InvalidOperationException();
               }
            }
            // Convert integral types to floats or doubles, some loss of precision is allowed.
            else if (!e1.IsIntegral())
               return f(e1, Expression.Convert(e2, e1.Type));
            else // e2 is not integral but e1 is
               return f(Expression.Convert(e1, e2.Type), e2);
         };

         #endregion

         expression = () => ifExpr();

         ifExpr = () =>
         {
            if (advanceIfMatch("if"))
            {
               var condition = expression();
               advanceWhitespace();

               var left = expression();
               advanceWhitespace();

               var right = advanceIfMatch("else") ? expression() : Expression.Default(left.Type);

               return Expression.Condition(convertToBool(condition), left, right);
            }
            else
               return condExpr();
         };

         condExpr = () =>
         {
            var result = nullExpr();

            advanceWhitespace();

            if (advanceIfMatch(@"\?"))
            {
               // Got conditional section.
               var left = expression();

               advanceWhitespace();

               expectAdvance(":");

               var right = expression();

               return Expression.Condition(convertToBool(result), left, right);
            }

            return result;
         };

         nullExpr = binaryOperator(() => orExpr, op((left, right) =>
                        Expression.Condition(
                           Expression.Equal(left, Expression.Constant(null)),
                           right, // if result is null return argument
                           left),
                        @"\?\?"));

         BinConversion binBoolConv = o => (left, right) => o(convertToBool(left), convertToBool(right));

         orExpr = binaryOperator(() => andExpr, op(binBoolConv(Expression.Or), @"\|\|", "or"));
         andExpr = binaryOperator(() => binOrExpr, op(binBoolConv(Expression.And), "&&", "and"));
         binOrExpr = binaryOperator(() => binExOrExpr, op(widenNums(Expression.Or), @"\|(?!\|)"));
         binExOrExpr = binaryOperator(() => binAndExpr, op(widenNums(Expression.ExclusiveOr), @"\^"));
         binAndExpr = binaryOperator(() => equalExpr, op(widenNums(Expression.And), "&(?!&)", "#")); // note our alias "flags" operator

         // We'll allow boxing here for value types simply because it results in *much* simpler code here.
         Func<Expression, Expression> toString = e => _GetExpr<Object, String>(Expression.Convert(e, typeof(Object)), o => o == null ? null : o.ToString());

         // Make an expression for string comparison.
         BinOp equals = null;
         equals = (left, right) =>
         {
            // Todo: we need to formulise this code a bit better and move it out.
            if (left.Type == typeof(String) || right.Type == typeof(String)) // note: one may represent null, which has Type not String
            {
               if (left.Type.IsEnum || right.Type.IsEnum)
                  return convEnums(equals)(left, right);

               left = toString(left);
               right = toString(right);

               var equalsMethod = typeof(String).GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, null, new[] { typeof(String), typeof(StringComparison) }, null);

               StringComparison strComparison;
               if (flags.HasFlag(ExpressionParsingFlags.UseCurrentCulture))
                  strComparison = flags.HasFlag(ExpressionParsingFlags.IgnoreCase) ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
               else
                  strComparison = flags.HasFlag(ExpressionParsingFlags.IgnoreCase) ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;

               return Expression.Condition(Expression.Equal(left, _nullStr), Expression.Equal(right, _nullStr), Expression.Call(left, equalsMethod, right, Expression.Constant(strComparison)));
            }
            else
            {
               BinOp eq = (l, r) =>
               {
                  if (l.Type.IsValueType && r.Type.IsValueType) return Expression.Equal(l, r);
                  l = Expression.Convert(l, typeof(Object)); r = Expression.Convert(r, typeof(Object)); // box both
                  return Expression.Condition(Expression.Equal(l, _null), Expression.Equal(r, _null), Expression.Equal(l, r));
               };
               return widenIfOneBool(widenNums(eq))(left, right); // default equals
            }
         };

         // Make an expression for regex comparison.
         BinOp regexComp = (left, right) =>
         {
            var isMatchMethod = typeof(Regex).GetMethod("IsMatch", BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, null, new[] { typeof(String) }, null);
            var nullToEmpty = typeof(Utils).GetMethod("NullToEmpty", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, new[] { typeof(String) }, null);

            // Note: relExpr() fetches the Regex on the "right", on which we'll call IsMatch.
            return Expression.Call(right, isMatchMethod, Expression.Call(null, nullToEmpty, left)); // call Regex.IsMatch(...)
         };

         equalExpr = binaryOperator(() => relExpr, op(equals, "=="), op((left, right) => Expression.Not(equals(left, right)), "!="),
                                                   op(regexComp, "=~"), op((left, right) => Expression.Not(regexComp(left, right)), "!~"),
                                                   op(equals, "=")); // allow = to work, as we won't allow assignment, if we do we should add a flag to enable that

         // Note that the operator order here is important, we should always scan for >= before >.
         relExpr = binaryOperator(() => addExpr, op(widenNums(Expression.LessThanOrEqual), "<="), op(widenNums(Expression.LessThan), "<"), op(widenNums(Expression.LessThanOrEqual), "≤"),
                                                 op(widenNums(Expression.GreaterThanOrEqual), ">="), op(widenNums(Expression.GreaterThan), ">"), op(widenNums(Expression.GreaterThan), "≥"));

         // Implement add, which is concatenation for strings.
         BinOp adder = (e1, e2) =>
         {
            if ((e1.Type == typeof(String) || e2.Type == typeof(String)))
               return Expression.Call(typeof(String).GetMethod("Concat", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(String), typeof(String) }, null),
                  toString(e1), toString(e2));
            else
               return Expression.Add(e1, e2);
         };

         addExpr = binaryOperator(() => multExpr, op(widenNums(adder), @"\+"), op(widenNums(Expression.Subtract), "-"));
         multExpr = binaryOperator(() => unaryExpr, op(widenNums(Expression.Multiply), @"\*"), op(widenNums(Expression.Divide), "/"), op(widenNums(Expression.Modulo), "%"));

         Func<Expression, Expression> convertToInt = e1 => _GetExpr<String, Int32>(e1, s => Int32.Parse(s));

         unaryExpr = () =>
         {
            if (advanceIfMatch("!")) return Expression.Not(convertToBool(unaryExpr()));

            // todo: allow + to convert strings to numbers? For now, does nothing.
            if (advanceIfMatch(@"\+")) return unaryExpr();

            if (advanceIfMatch("-")) return Expression.Negate(unaryExpr());

            if (advanceIfMatch("~")) return Expression.Not(unaryExpr());

            return primaryExpr();
         };

         primaryExpr = () =>
         {
            var result = unitExpr(); // get literal "on left"

            advanceWhitespace();

            /* Resolve something like a.b.c().
             * 1. if a is a member of context, .b is member access etc.
             * 2. a.b.c() is a static method call (there is no member b of a), we explicitly do *not* support this.
             *    Our expressions are meant to be relative to a context and to "restrict" their power, only method calls allowed through the context are accepted.
             *    In order to allow for the use of enums, however, we'll allow static fields and properties.
             * 3. otherwise we need to resolve a static value, either a.b (to allow for method call) or a.b.c in case of static delegate call.
             *    We will resolve this from right to left, i.e. a.b.c before a.b. */
            var identifier = "";
            if (result is _IdentifierExpression)
            {
               // Resolve the identifier to a value.
               identifier = ((_IdentifierExpression)result).Identifier;

               if (identifier.Contains("."))
               {
                  var parts = identifier.Split('.');
                  var periodsLeft = 0;

                  for (var j = parts.Length - 1; j >= 0; j--)
                  {
                     result = nameResolver(actualContextExpr, parts[j], j == 0 ? null : String.Join(".", parts, 0, j), defaultResolver);
                     if (result == null) continue;
                     periodsLeft = parts.Length - j - 1; break;
                  }

                  // Backtrack for continued parsing below.
                  for (var p = periodsLeft; p > 0; p--)
                  {
                     i -= 1;
                     while (expr[i] != '.') i--;
                  }

                  if (result == null) throwExpected("known name, could not resolve '{0}'", identifier);
                  identifier = "";
               }
               else if (i >= expr.Length || expr[i] != '(')
               {
                  // Simple name without periods, but not a method call.
                  // See above, because the default resolver is used for qualified names first we must try that in order to be consistent.
                  result = nameResolver(actualContextExpr, identifier, null, defaultResolver);
                  if (result == null) throwExpected("known name, could not resolve '{0}'", identifier);
                  identifier = "";
               }
               // else: method call, will handle below
            }

            for (; ; )
            {
               advanceWhitespace();

               if (advanceIfMatch("\\.")) // member access
               {
                  Utils.DebugAssert(identifier.Length == 0);

                  identifier = nameToken();

                  advanceWhitespace(); // to check for (

                  if (i < expr.Length && expr[i] == '(')
                     continue; // let method call handle this

                  // We consider the . operator to imply property or field access.
                  // Allowing for custom resolving here gives strange edge cases (e.g. what if the instance
                  // is a context, it is not possible to tell at compile time).
                  result = defaultResolver(result, identifier, null, defaultResolver);
                  identifier = "";
               }
               else if (advanceIfMatch("\\[")) // array accessor
               {
                  Utils.DebugAssert(identifier.Length == 0);

                  // Todo: could go wild here too.. indexers with multiple arguments.
                  var index = expression();

                  expectAdvance("\\]");

                  if (typeof(Array).IsAssignableFrom(result.Type)) // it's an array access
                     result = Expression.ArrayIndex(result, index);
                  else // property index
                  {
                     var indexProp = result.Type.GetProperty("Item", new[] { index.Type });
                     if (indexProp == null)
                        indexProp = result.Type.GetProperty("Chars", new[] { index.Type });
                     if (indexProp == null)
                        throw SyntaxException.Create(expr, i, "Cannot find indexer, tried 'Item' and 'Chars', others not (yet) supported.");
                     result = Expression.MakeIndex(result, indexProp, new[] { index });
                  }
               }
               else if (advanceIfMatch("\\(")) // invocation
               {
                  var parameters = new List<Expression>();

                  if (i < expr.Length && expr[i] != ')')
                     for (; ; )
                     {
                        advanceWhitespace();
                        parameters.Add(expression());
                        advanceWhitespace();
                        if (advanceIfMatch(",")) continue;
                        break;
                     }

                  expectAdvance(@"\)");

                  // Resolve method call. For now non-static calls only.
                  if (identifier.Length > 0)// identifier must have been set above
                  {
                     // NOTE: we are looking for exact types here, so no type coercion here.
                     // This is much harder than it looks because of things like overload resolution, so won't support for now (if ever).
                     var methodTarget = result is _IdentifierExpression ? actualContextExpr : result;
                     var method = (result is _IdentifierExpression ? actualContextType : result.Type)
                                 .GetMethod(identifier, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, null, (from e in parameters select e.Type).ToArray(), null);
                     if (method != null)
                     {
                        result = Expression.Call(method.IsStatic ? null : methodTarget, method, parameters);
                        identifier = "";
                        continue;
                     }

                     // Now we know the name is not a method, we can resolve it to a delegate.
                     if (result is _IdentifierExpression)
                        result = nameResolver(methodTarget, identifier, null, defaultResolver);
                     else
                        result = defaultResolver(methodTarget, identifier, null, defaultResolver);

                     if (result == null) throwExpected("name resolving to a delegate ('{0}' did not resolve or value is not a delegate) - note that method arguments must be of the correct type and are *not* coerced currently", identifier);
                  }

                  // Either we previously resolved a delegate, or we just determined the identifier was not a method.
                  if (typeof(Delegate).IsAssignableFrom(result.Type)) // it's a delegate that was already resolved, call that
                  {
                     var method = result.Type.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, null, (from e in parameters select e.Type).ToArray(), null);
                     if (method == null)
                     {
                        var invoke = result.Type.GetMethod("Invoke");
                        throwExpected("correct delegate invocation, found delegate requiring {0} parameters of types {1}", invoke.GetParameters().Length, String.Join(",", invoke.GetParameters().Select(p => p.ParameterType.Name)));
                     }
                     result = Expression.Call(result, method, parameters);
                  }
                  else if (identifier.Length == 0) throwExpected("expression to resolve to a delegate value, got value of type '{0}'", result.Type.FullName);
                  else throwExpected("'{0}' to resolve to a delegate or method", identifier);

                  identifier = "";
               }
               else break; // nothing to see here folks
            }

            return result;
         };

         unitExpr = () =>
         {
            Expression unitResult = null;

            if (i == expr.Length)
               throw SyntaxException.Create(expr, expr.Length, "Unexpected end of input");

            // Parenthesized expression.
            if (advanceIfMatch(@"\("))
            {
               unitResult = expression();
               expectAdvance(@"\)");
            }
            else
            {
               var token = "";
               var startChar = expr[i];
               if (startChar == '\'' || startChar == '"')
               {
                  i += 1; // skip start quote
               GetString: token += advanceWhile(() => expr[i] != startChar && expr[i] != '`'); // our string escape character is ` like PowerShell
                  if (i < expr.Length && expr[i] == '`')
                  {
                     if (i >= expr.Length - 1) throwExpected("valid string escape sequence");
                     switch (expr[i + 1])
                     {
                        case 'n': token += '\n'; break;
                        case 'r': token += '\r'; break;
                        case 't': token += '\t'; break;
                        case '"': token += '"'; break;
                        case '`': token += '`'; break;
                        case '\\': token += '\\'; break;
                        case '\'': token += '\''; break;
                        default: token += expr[i + 1]; break;
                     }
                     i += 2;
                     goto GetString;
                  }

                  expectAdvance(startChar.ToString()); // matching end quote
                  unitResult = Expression.Constant(token);
               }
               else if (startChar == '/') // regex
               {
                  i += 1; // skip /
                  for (; i < expr.Length && expr[i] != startChar; i++)
                     if (expr[i] == '\\' && i < expr.Length - 1 && expr[i + 1] == '/') // allow escaping of /, other escaping is left to regex engine
                     {
                        token += '/';
                        i += 1;
                     }
                     else
                        token += expr[i];

                  expectAdvance("/");

                  var options = flags.HasFlag(ExpressionParsingFlags.UseCurrentCulture) ? RegexOptions.None : RegexOptions.CultureInvariant;
                  if (flags.HasFlag(ExpressionParsingFlags.IgnoreCase)) options |= RegexOptions.IgnoreCase;

                  // Read modifiers.
                  for (; i < expr.Length; i += 1)
                     switch (expr[i])
                     {
                        case 'i':
                           options |= RegexOptions.IgnoreCase;
                           continue;

                        case 'I': // undoes IgnoreCase (e.g. when set with flag)
                           options &= ~RegexOptions.IgnoreCase;
                           continue;

                        case 's':
                           options |= RegexOptions.Singleline;
                           continue;

                        case 'x':
                           options |= RegexOptions.IgnorePatternWhitespace;
                           continue;

                        default:
                           goto EndOptions;
                     }

               EndOptions:
                  unitResult = Expression.New(typeof(Regex).GetConstructor(new[] { typeof(String), typeof(RegexOptions) }), Expression.Constant(token), Expression.Constant(options));
               }
               else if (Char.IsNumber(startChar) || startChar == '.')
               {
                  var isDouble = false;

                  var isHex = startChar == '0' && i < expr.Length - 1 && (expr[i + 1] == 'x' || expr[i + 1] == 'X');
                  if (isHex) i += 2; // skip 0x

                  // Parse an int or double according to (from c# spec:)
                  //real-literal:: 
                  //   decimal-digits   .   decimal-digits   exponent-partopt
                  //      real-type-suffixopt
                  //   .   decimal-digits   exponent-partopt
                  //      real-type-suffixopt
                  //   decimal-digits   exponent-part   real-type-suffixopt
                  //   decimal-digits   real-type-suffix 
                  // Note: we ignore real-type-suffixes for now
                  for (; i < expr.Length && (Char.IsNumber(expr[i]) || expr[i] == '.' || expr[i] == 'e' || expr[i] == 'E' ||
                                            (isHex && ((expr[i] >= 'a' && expr[i] <= 'f') || (expr[i] >= 'A' && expr[i] <= 'F')))); i++) // simple numbers for now.. can expand later, invariant culture
                  {
                     if (expr[i] == '.')
                     {
                        if (isDouble) break;
                        isDouble = true;
                     }

                     token += expr[i];

                     if (expr[i] == 'e' || expr[i] == 'E')
                     {
                        isDouble = true;
                        if (i < expr.Length - 1 && (expr[i + 1] == '-' || expr[i + 1] == '+'))
                        {
                           i += 1;
                           token += expr[i];
                        }
                     }
                  }

                  // We could support F#'s y and s, but I think that's going a bit too far probably.
                  var suffix = optAdvance("[mMdDfF]|([uU][lL]?)|([lL][uU]?)");
                  Type numType;
                  if (suffix == null)
                     numType = isDouble ? typeof(Double) : typeof(Int32);
                  else
                     switch (suffix = suffix.ToUpperInvariant())
                     {
                        case "M": numType = typeof(Decimal); isDouble = true; break;
                        case "D": numType = typeof(Double); isDouble = true; break;
                        case "F": numType = typeof(Single); isDouble = true; break;
                        default:
                           if (suffix.Contains("L")) numType = suffix.Contains("U") ? typeof(UInt64) : typeof(Int64);
                           else numType = suffix.Contains("U") ? typeof(UInt32) : typeof(Int32);
                           break;
                     }

                  if (isHex && isDouble)
                     throwExpected("either hex or double number, got '{0}'", token);

                  var parseMethod = numType.GetMethods().Where(m => m.Name == "TryParse" && m.GetParameters().Length == 4).Single(); // todo?
                  var numFlags = isDouble ? NumberStyles.Float : (isHex ? NumberStyles.HexNumber : NumberStyles.Integer);

                  var args = new Object[] { token, numFlags, NumberFormatInfo.InvariantInfo, null };
                  if (!(Boolean)parseMethod.Invoke(null, args))
                     throwExpected("valid number, got '{0}' - expected a {1}number of type {2}", token, isHex ? "hex " : "", numType.FullName);

                  unitResult = Expression.Constant(args[3], numType);
               }
               else if (startChar == '∞')
               {
                  unitResult = Expression.Constant(Double.PositiveInfinity);
                  i += 1;
               }
               else if (startChar == 'π')
               {
                  unitResult = Expression.Constant(Math.PI);
                  i += 1;
               }
               else
               {
                  var first = true;
                  for (; ; )
                  {
                     advanceWhitespace();
                     var name = nameToken();

                     if (first)
                     {
                        if (name == "true")
                           return _true;
                        else if (name == "false")
                           return _false;
                        else if (name == "null")
                           return Expression.Constant(null);
                        else if (name == "this")
                           return actualContextExpr;
                        else if (name == "base")
                           return Expression.Convert(actualContextExpr, actualContextExpr.Type.BaseType);

                        first = false;
                     }

                     if (name.Length == 0) throwExpected("identifier");
                     token += name;
                     advanceWhitespace();
                     if (advanceIfMatch(@"\.")) token += ".";
                     else break;
                  }

                  unitResult = new _IdentifierExpression { Identifier = token };
               }
            }

            return unitResult;
         };

         nameToken = () =>
         {
            advanceWhitespace();

            if (i < expr.Length && Char.IsNumber(expr[i])) throwExpected("name which cannot start with a number");

            // NOTE: we allow $ here, C# does not.
            // This code should match IsValidName below.
            return advanceWhile(() => Char.IsLetter(expr[i]) || Char.IsNumber(expr[i]) || expr[i] == '_' || expr[i] == '$');
         };

         #endregion

         try
         {
            // Start actual parsing, begin by stripping initial whitespace.
            advanceWhitespace();

            // If the whole expression is empty (or just whitespace and comments), we'll consider that an expression with value "".
            var allowEmpty = i == expr.Length && (flags & ExpressionParsingFlags.EmptyExpressionIsEmptyString) != 0;
            var body = allowEmpty ? Expression.Constant("") : expression();

            // The actual type of the expression. E.g. when using an IEnumerable<T> TOutput may only be IEnumerable<Object>
            // but we have inferred the *actual* type so return that too.
            var bodyType = body.Type;

            if (i != expr.Length)
               throw SyntaxException.Create(expr, i, "Expected end of input.");

            if (typeof(TOutput) == typeof(Boolean))
               body = convertToBool(body);
            else if (typeof(TOutput) == typeof(String) && body.Type != typeof(String))
               body = toString(body);
            else // Attempt a cast, e.g. int > double, this may need more work.
               body = Expression.Convert(body, typeof(TOutput));

            var result = Expression.Lambda<Func<TContext, TOutput>>(body, contextExpr);

            // If this expression is culture independent, we do this by setting the current thread's culture.
            if ((flags & ExpressionParsingFlags.UseCurrentCulture) == 0)
            {
               Func<Func<TContext, TOutput>, Func<TContext, TOutput>> tryFin = b => c =>
               {
                  CultureInfo currentCulture = null;
                  try
                  {
                     currentCulture = Thread.CurrentThread.CurrentCulture;
                     Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                     return b(c);
                  }
                  finally
                  {
                     if (currentCulture != null) Thread.CurrentThread.CurrentCulture = currentCulture;
                  }
               };

               result = _WrapExprInline<TContext, TOutput>(result, f => tryFin(f));
            }

            return new CompiledExpression<TContext, TOutput>
            {
               Ast = result,
               OutputType = bodyType
            };
         }
         catch (SyntaxException) { throw; }
         catch (Exception e)
         {
            throw SyntaxException.Create(e, expr, "Unexpected error occurred parsing or compiling expression");
         }
      }

      // *NOTE* This code must match the parser above
      // todo: calling this isn't nice because the generic arguments are required for the call but not actually needed. Move somewhere else.
      public static Boolean IsValidName(String name)
      {
         if (String.IsNullOrWhiteSpace(name)) return false;
         if (Char.IsNumber(name[0])) return false;
         return name.All(c => Char.IsNumber(c) || Char.IsLetter(c) || c == '_' || c == '$');
      }

      internal class _ExpressionDebugContext
      {
         private readonly Func<TContext, TOutput> _mWrappedFunc;
         public readonly String Expression;

         public _ExpressionDebugContext(String expr, Func<TContext, TOutput> func)
         {
            _mWrappedFunc = func;
            Expression = expr;
         }

         // Show the expression which is used by the debugger to view "this".
         public override String ToString()
         {
            return Expression;
         }

         public TOutput RunFunc(TContext input)
         {
            return _mWrappedFunc(input);
         }
      }
   }
}
