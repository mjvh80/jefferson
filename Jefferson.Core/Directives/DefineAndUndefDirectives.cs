using Jefferson.Output;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public class DefineDirective : IDirective
   {
      public String Name
      {
         get { return "define"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public Boolean MayBeEmpty
      {
         get { return true; }
      }

      private Tuple<String, String>[] _ParseParameters(Parsing.TemplateParserContext parserContext, String input, out String name)
      {
         var idx = input.IndexOf('(');
         if (idx < 0)
         {
            name = input;
            return null;
         }

         var lidx = input.IndexOf(')', idx + 1);
         if (lidx < 0) throw parserContext.SyntaxError(idx, "Missing ')'.");

         if (lidx < input.Length - 1 && input.Substring(lidx + 1).Trim().Length != 0)
            throw parserContext.SyntaxError(idx, "expected end of parameter input");

         name = input.Substring(0, idx);

         if (lidx == idx + 1)
            return new Tuple<String, String>[0];

         var @params = new List<Tuple<String, String>>();
         for (var i = idx + 1; i < input.Length; )
         {
            var endIdx = input.IndexOf(',', i);
            if (endIdx < 0) endIdx = lidx;

            var argStr = input.Substring(i, endIdx - i).Trim();
            @params.Add(_ParseTypedName(argStr));

            if (endIdx == lidx) break;
            i = endIdx + 1;
         }
         return @params.ToArray();
      }

      /// <summary>
      /// Parses an expression of the form [type]? name. If type is not specified it defaults to System.String.
      /// </summary>
      private static Tuple<String, String> _ParseTypedName(String argStr)
      {
         var spaceIdx = argStr.IndexOf(' ');
         if (spaceIdx > 0 && spaceIdx < argStr.Length - 1)
            return Tuple.Create(argStr.Substring(0, spaceIdx), argStr.Substring(spaceIdx + 1));
         else
            return Tuple.Create("System.String", argStr); // default type is string
      }

      private static String _CSharpToDotNetType(String type, Boolean ignoreCase)
      {
         if (ignoreCase)
            type = type.ToLowerInvariant();

         switch (type)
         {
            case "int": return "System.Int32";
            case "uint": return "System.UInt32";
            case "long": return "System.Int64";
            case "ulong": return "System.UInt64";
            case "short": return "System.Int16";
            case "ushort": return "System.UInt16";
            case "bool": return "System.Boolean";
            case "decimal": return "System.Decimal";
            case "byte": return "System.Byte";
            case "sbyte": return "System.SByte";
            case "string": return "System.String";
            case "float": return "System.Single";
            case "double": return "System.Double";
            case "object": return "System.Object";
            default: return type;
         }
      }

      public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         // Compile variables.
         var compiledVars = new Dictionary<String, CompiledExpression<Object, Object>>();

         var haveSource = source != null; // empty string is empty body

         for (var startIdx = 0; ; )
         {
            var varSepIdx = arguments.IndexOf(';', startIdx);

            // ; can only be used for multiple key value pair style definitions.
            var bindingLen = (varSepIdx < 0 ? arguments.Length : varSepIdx) - startIdx;
            if (bindingLen == 0) throw parserContext.SyntaxError(startIdx, "Invalid variable binding found: empty.");

            var binding = arguments.Substring(startIdx, bindingLen).Trim();
            if (binding.Length == 0) throw parserContext.SyntaxError(startIdx, "Invalid variable binding found: empty.");

            var eqIdx = binding.IndexOf('=');

            var name = eqIdx < 0 ? binding.Substring(0) : binding.Substring(0, eqIdx);
            name = name.Trim();

            var @params = _ParseParameters(parserContext, name, out name);
            var typedName = _ParseTypedName(name);
            name = typedName.Item2;

            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(0, "Variable '{0}' has an invalid name.", name);

            if (eqIdx >= 0 && @params == null)
            {
               if (haveSource)
                  throw parserContext.SyntaxError(startIdx, "#define directive is not empty, unexpected '='");

               var value = binding.Substring(eqIdx + 1).Trim();
               compiledVars.Add(name, parserContext.CompileExpression<Object>(value));
            }
            else
            {
               if (eqIdx < 0 && !haveSource)
                  throw parserContext.SyntaxError(startIdx, "Missing #define body.");

               if (eqIdx >= 0 && haveSource)
                  throw parserContext.SyntaxError(startIdx, "Unexpected #define body.");

               var haveParams = @params != null;

               _ParameterBinder paramBinder = null;
               if (haveParams)
               {
                  paramBinder = new _ParameterBinder();
                  paramBinder.ParamDecls = new Dictionary<String, ParameterExpression>(@params.Length, parserContext.Options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                  foreach (var p in @params)
                  {
                     var paramType = Type.GetType(_CSharpToDotNetType(p.Item1, parserContext.Options.IgnoreCase), throwOnError: false, ignoreCase: parserContext.Options.IgnoreCase);
                     if (paramType == null)
                        throw parserContext.SyntaxError(startIdx, "Could not resolve parameter type '{0}'", p.Item1);
                     if (!ExpressionParser<Object, Object>.IsValidName(p.Item2))
                        throw parserContext.SyntaxError(startIdx, "Invalid parameter name '{0}'", p.Item2); // todo: better positional error
                     paramBinder.ParamDecls.Add(p.Item2, Expression.Variable(paramType, p.Item2));
                  }

                  paramBinder.WrappedBinder = parserContext.ReplaceCurrentVariableBinder(paramBinder);
               }

               // Add an expression to get and compile at runtime.
               // Todo: this could perhaps cache, if used more than once.
               // Todo(2): can probably be done more efficiently as we don't need the compiledexpression step, so we have some
               // extra lambda invocation overhead.
               var sb = Expression.Variable(typeof(StringBuilderOutputWriter));
               var ctxParam = Expression.Parameter(typeof(Object));

               if (haveParams)
               {
                  CompiledExpression<Object, Object> @value = null;
                  Type returnType = typeof(String);
                  if (eqIdx > 0)
                  {
                     var rt = _CSharpToDotNetType(typedName.Item1, parserContext.Options.IgnoreCase);
                     if (rt == "System.String")
                        @value = parserContext.CompileExpression<Object>(binding.Substring(eqIdx + 1).Trim());
                     else
                     {
                        // NOTE: the expression parser *knows* the actual outputtype, and returns it.
                        // As we don't know it during C# compile time we specify Object and perform the conversion below. Thus this
                        // conversion won't fail at runtime!
                        @value = parserContext.CompileExpression<Object>(binding.Substring(eqIdx + 1).Trim());
                        returnType = Type.GetType(rt, throwOnError: true, ignoreCase: parserContext.Options.IgnoreCase); // todo: errors
                     }
                  }
                  else if (typedName.Item1 != "System.String")
                     throw parserContext.SyntaxError(startIdx, "Unexpected return type specification. Because #define has a body, return type is always System.String.");

                  Type funcType;
                  switch (@params.Length)
                  {
                     case 0: funcType = typeof(Func<>); break;
                     case 1: funcType = typeof(Func<,>); break;
                     case 2: funcType = typeof(Func<,,>); break;
                     case 3: funcType = typeof(Func<,,,>); break;
                     case 4: funcType = typeof(Func<,,,,>); break;
                     case 5: funcType = typeof(Func<,,,,,>); break;
                     default:
                        throw parserContext.SyntaxError(0, "#define directives support up to 5 parameters currently.");
                  }

                  funcType = funcType.MakeGenericType(paramBinder.ParamDecls.Select(p => p.Value.Type).Concat(new[] { returnType }).ToArray());

                  // Ensure that e.g. string x() = 1 works (or more common x() = 1).
                  var converter = @value == null ? null : TypeUtils.GetConverter(@value.OutputType, returnType, parserContext.Options.IgnoreCase);

                  compiledVars.Add(name, new CompiledExpression<Object, Object>
                  {
                     Ast = Expression.Lambda<Func<Object, Object>>(
                        // The result *value* here is not a string, but e.g. a Func<String, String> (for one parameter).
                        // Thus when the define is used, the Func is evaluated by the expression parser when it invokes the lambda.
                              Expression.Lambda(funcType,
                                 @value == null
                                 ?
                        // The result is the body evaluated.
                                 (Expression)Expression.Block(new[] { sb },
                                      Expression.Assign(sb, Expression.New(typeof(StringBuilderOutputWriter))),
                                      Expression.Invoke(parserContext.Parse<Object>(source) /* PARSED BODY */, ctxParam, sb),
                                         Expression.Call(sb, Utils.GetMethod<StringBuilderOutputWriter>(s => s.GetOutput())))
                                 :
                        // The result is an inline function. Invoke it and convert the type.
                        // Note that this type conversion will not fail at runtime!
                                 converter(Expression.Convert(Expression.Invoke(@value.Ast, Expression.Convert(ctxParam, typeof(Object))), @value.OutputType)),

                                 paramBinder.ParamDecls.Values),
                              ctxParam),
                     OutputType = funcType
                  });
               }
               else
               {
                  Debug.Assert(@params == null);
                  compiledVars.Add(name, new CompiledExpression<Object, Object>
                  {
                     Ast = Expression.Lambda<Func<Object, Object>>(
                              Expression.Block(new[] { sb },
                                 Expression.Assign(sb, Expression.New(typeof(StringBuilderOutputWriter))),
                                 Expression.Invoke(parserContext.Parse<Object>(source) /* PARSED BODY */, ctxParam, sb),
                                 Expression.Convert(
                                    Expression.Call(sb, Utils.GetMethod<StringBuilderOutputWriter>(s => s.GetOutput())),
                                    typeof(Object))),
                              ctxParam),
                     OutputType = typeof(String)
                  });
               }

               if (haveParams)
               {
                  parserContext.ReplaceCurrentVariableBinder(paramBinder.WrappedBinder);
               }
            }

            if (varSepIdx < 0) break;

            if (haveSource)
               throw parserContext.SyntaxError(varSepIdx, "Unexpected ';', #define is not empty.");

            startIdx = varSepIdx + 1;
         }

         var currentContext = parserContext.GetNthContext(0);
         var body = new List<Expression>(compiledVars.Count + 1);

         foreach (var kvp in compiledVars)
         {
            var valExpr = Expression.Convert(Expression.Invoke(compiledVars[kvp.Key].Ast, Expression.Convert(currentContext, typeof(Object))), kvp.Value.OutputType);
            body.Add(parserContext.SetVariable(currentContext, kvp.Key, 0, valExpr)); // todo: track positions of variables for better errors
         }

         return Expression.Block(body);
      }

      private class _ParameterBinder : IVariableBinder
      {
         public Dictionary<String, ParameterExpression> ParamDecls;
         public IVariableBinder WrappedBinder;

         // Update variable declaration to compile the variable name.
         public Expression BindVariableRead(Expression currentContext, String name)
         {
            ParameterExpression variable;
            if (ParamDecls.TryGetValue(name, out variable))
               return variable;

            return WrappedBinder == null ? null : WrappedBinder.BindVariableRead(currentContext, name);
         }

         public Expression BindVariableWrite(Expression currentContext, String name, Expression value)
         {
            // todo: this error sucks, because it's not clear where in the source this is, we need more context
            if (ParamDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot set variable '{0}' because it has been bound to a define parameter.", name);

            return WrappedBinder == null ? null : WrappedBinder.BindVariableWrite(currentContext, name, value);
         }

         public Expression UnbindVariable(Expression currentContext, String name)
         {
            if (ParamDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot unset variable '{0}' because it has been bound to a define parameter.", name);

            return WrappedBinder == null ? null : WrappedBinder.UnbindVariable(currentContext, name);
         }
      }
   }

   public class UndefDirective : IDirective
   {
      public String Name
      {
         get { return "undef"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public Boolean MayBeEmpty
      {
         get { return true; }
      }

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         var args = arguments.Split(new[] { ';' }).Select(n => n.Trim()).Where(n => n.Length > 0);
         foreach (var name in args)
            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(0, "Variable '{0}' has an invalid name.", name);

         var currentContext = parserContext.GetNthContext(0);
         var body = new List<Expression>();

         foreach (var name in args)
            body.Add(parserContext.RemoveVariable(currentContext, name, 0)); // todo track position of names for somewhat better errors

         if (body.Count == 0)
            throw parserContext.SyntaxError(0, "#undef requires arguments (variables to undefine)");

         return Expression.Block(body);
      }
   }
}
