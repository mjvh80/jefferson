using Jefferson.Extensions;
using Jefferson.Output;
using Jefferson.Parsing;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public class DefineDirective : IDirective
   {
      /// <summary>
      /// Indicates an output statement is required. Note that if syntax a=b is used no explicit #out is used.
      /// </summary>
      protected readonly Boolean mRequireOutStatement;

      /// <summary>
      /// Output statement is optional.
      /// </summary>
      protected readonly Boolean mAllowOutStatement;

      public DefineDirective() : this("define", false, false) { }

      protected internal DefineDirective(String name, Boolean allowOut, Boolean requireOut)
      {
         Contract.Requires(!String.IsNullOrEmpty(name));

         mRequireOutStatement = requireOut;
         mAllowOutStatement = allowOut;

         Name = name;
         ReservedWords = mAllowOutStatement ? new[] { "out" } : null;
      }

      public String Name { get; private set; }

      public String[] ReservedWords { get; private set; }

      public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         if (String.IsNullOrWhiteSpace(arguments))
            throw parserContext.SyntaxError(0, "Expected a name to bind to something");

         var compiledVars = new Dictionary<String, CompiledExpression<Object, Object>>();
         var haveSource = source != null; // note: empty string is empty body
         var eqIdx = arguments.IndexOf('=', 0);
         var body = source;
         var @outBody = (String)null;
         var haveOutStmt = false;

         if (haveSource && mAllowOutStatement)
         {
            var outIdx = parserContext.FindDirectiveEnd(source, 0, "$$#out");

            body = outIdx < 0 ? source : source.Substring(0, outIdx);
            @outBody = outIdx < 0 ? null : source.Substring(source.IndexOf("$$", outIdx + 2) + 2);
            haveOutStmt = outIdx >= 0;

            if (eqIdx >= 0)
               @outBody = body;
         }

         if (!haveSource && mRequireOutStatement)
            throw parserContext.SyntaxError(0, "Missing body (including #out).");

         // todo: could optimize this and remove this in some cases:
         // - it's not needed if we don't have params and are not a let-binding.
         var paramBinder = CreateVariableBinder(parserContext);
         paramBinder.ParamDecls = new Dictionary<String, ParameterExpression>(0, parserContext.Options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
         paramBinder.WrappedBinder = parserContext.ReplaceCurrentVariableBinder(paramBinder);

         for (var startIdx = 0; startIdx < arguments.Length; )
         {
            var name = eqIdx < 0 ? arguments : arguments.Substring(startIdx, eqIdx - startIdx);
            name = name.Trim();

            if (String.IsNullOrEmpty(name))
               throw parserContext.SyntaxError(startIdx, "Expected a name to bind to something");

            var @params = _ParseParameters(parserContext, name, out name);
            var typedName = _ParseTypedName(name);
            name = typedName.Item2;

            // Clear parameters from a previous definition.
            paramBinder.ParamDecls.Clear();

            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(startIdx, "Variable '{0}' has an invalid name.", name);

            // Look for =?. Note that = ? should not work.
            var allowUnknownNames = eqIdx >= 0 && arguments.At(eqIdx + 1) == '?';
            var oldOverride = parserContext.OverrideAllowUnknownNames;
            if (allowUnknownNames) parserContext.OverrideAllowUnknownNames = allowUnknownNames;

            if (eqIdx >= 0 && @params == null)
            {
               if (haveSource && !mAllowOutStatement)
                  throw parserContext.SyntaxError(startIdx, "#{0} directive is not empty, unexpected '='", Name);

               if (haveOutStmt) // here the body *is* out
                  throw parserContext.SyntaxError(0, "Unexpected #out statement."); // < todo location

               compiledVars.Add(name, parserContext.CompileExpression<Object>(arguments, allowUnknownNames ? eqIdx + 2 : eqIdx + 1, out startIdx));
            }
            else
            {
               if (eqIdx < 0 && !haveSource)
                  throw parserContext.SyntaxError(startIdx, "Missing #{0} body.", Name);

               if (eqIdx >= 0 && haveSource && !mAllowOutStatement)
                  throw parserContext.SyntaxError(startIdx, "Unexpected #{0} body.", Name);

               if (mRequireOutStatement && !haveOutStmt)
                  throw parserContext.SyntaxError(startIdx, "Missing $$#out$$.");

               if (@params != null)
                  foreach (var p in @params)
                  {
                     var paramType = Type.GetType(TypeUtils.CSharpToDotNetType(p.Item1, parserContext.Options.IgnoreCase), throwOnError: false, ignoreCase: parserContext.Options.IgnoreCase);
                     if (paramType == null)
                        throw parserContext.SyntaxError(startIdx, "Could not resolve parameter type '{0}'", p.Item1);
                     if (!ExpressionParser<Object, Object>.IsValidName(p.Item2))
                        throw parserContext.SyntaxError(startIdx, "Invalid parameter name '{0}'", p.Item2); // todo: better positional error

                     paramBinder.ParamDecls.Add(p.Item2, Expression.Variable(paramType, p.Item2));
                  }

               // Add an expression to get and compile at runtime.
               // Todo: this could perhaps cache, if used more than once.
               // Todo(2): can probably be done more efficiently as we don't need the compiledexpression step, so we have some
               // extra lambda invocation overhead.
               var sb = Expression.Variable(typeof(StringBuilderOutputWriter));
               var ctxParam = Expression.Parameter(typeof(Object));

               var rt = TypeUtils.CSharpToDotNetType(typedName.Item1, parserContext.Options.IgnoreCase);
               if (eqIdx < 0 && rt != "System.String")
                  throw parserContext.SyntaxError(startIdx, "Unexpected return type specification. Because #{0} has a body, return type is always System.String.", Name);

               Type funcType = null;
               CompiledExpression<Object, Object> @value = null;
               Type returnType = null;
               if (@params != null)
               {
                  returnType = typeof(String);

                  if (eqIdx > 0)
                  {
                     // NOTE: the expression parser *knows* the actual outputtype, and returns it.
                     // As we don't know it during C# compile time we specify Object and perform the conversion below. Thus this
                     // conversion won't fail at runtime!
                     @value = parserContext.CompileExpression<Object>(arguments, allowUnknownNames ? eqIdx + 2 : eqIdx + 1, out startIdx);
                     returnType = Type.GetType(rt, throwOnError: true, ignoreCase: parserContext.Options.IgnoreCase); // todo: errors
                  }

                  switch (@params.Length)
                  {
                     case 0: funcType = typeof(Func<>); break;
                     case 1: funcType = typeof(Func<,>); break;
                     case 2: funcType = typeof(Func<,,>); break;
                     case 3: funcType = typeof(Func<,,,>); break;
                     case 4: funcType = typeof(Func<,,,,>); break;
                     case 5: funcType = typeof(Func<,,,,,>); break;
                     default:
                        throw parserContext.SyntaxError(0, "#{0} directives support up to 5 parameters currently.", Name);
                  }

                  funcType = funcType.MakeGenericType(paramBinder.ParamDecls.Select(p => p.Value.Type).Concat(new[] { returnType }).ToArray());
               }

               // Ensure that e.g. string x() = 1 works (or more common x() = 1).
               var converter = @value == null ? null : TypeUtils.GetConverter(@value.OutputType, returnType, parserContext.Options.IgnoreCase);

               var bodyExpr = @value == null
                              ? // The result is the body evaluated.
                              (Expression)Expression.Block(new[] { sb },
                                   Expression.Assign(sb, Expression.New(typeof(StringBuilderOutputWriter))),
                                   Expression.Invoke(parserContext.Parse<Object>(body) /* PARSED BODY */, ctxParam, sb),
                                      Expression.Call(sb, Utils.GetMethod<StringBuilderOutputWriter>(s => s.GetOutput())))
                              : /* 
                                 * The result is an inline function. Invoke it and convert the type.
                                 * Note that this case is only used if parameters are used.
                                 */
                              converter(Expression.Convert(Expression.Invoke(@value.Ast, Expression.Convert(ctxParam, typeof(Object))), @value.OutputType));

               compiledVars.Add(name, new CompiledExpression<Object, Object>
               {
                  Ast = Expression.Lambda<Func<Object, Object>>(
                           @params == null ?
                              bodyExpr :
                     /* The result *value* here is not a string, but e.g. a Func<String, String> (for one parameter).
                      * Thus when the define is used, the Func is evaluated by the expression parser when it invokes the lambda. */
                              Expression.Lambda(funcType, bodyExpr, paramBinder.ParamDecls.Values),
                        ctxParam),
                  OutputType = funcType ?? typeof(String)
               });
            }

            parserContext.OverrideAllowUnknownNames = oldOverride;

            // Skip ; and whitespace, allow empty ;. Require at least one ;?
            if (eqIdx >= 0)
            {
               while (arguments.At(startIdx) == ';' || Char.IsWhiteSpace(arguments.At(startIdx)))
                  startIdx += 1;

               if (startIdx < arguments.Length)
               {
                  eqIdx = arguments.IndexOf('=', startIdx);
                  if (eqIdx < 0) throw parserContext.SyntaxError(startIdx, "Expected end of directive argument input");
               }
            }
            else
               break;
         }

         // At this point parameters have bound and should no longer be available. 
         paramBinder.ParamDecls.Clear();

         var currentContext = parserContext.GetNthContext(0);
         var bodyStmt = new List<Expression>(compiledVars.Count + 1);
         var bodyLocals = new List<ParameterExpression>();

         foreach (var kvp in compiledVars)
         {
            var valExpr = Expression.Convert(Expression.Invoke(compiledVars[kvp.Key].Ast, Expression.Convert(currentContext, typeof(Object))), kvp.Value.OutputType);
            bodyStmt.Add(MakeSetVariableExpr(parserContext, currentContext, paramBinder, kvp.Key, 0, valExpr, bodyLocals));
         }

         // If we have an #out, add it.
         if (@outBody != null)
            bodyStmt.Add(Expression.Invoke(parserContext.Parse<Object>(@outBody), currentContext, parserContext.Output));

         parserContext.ReplaceCurrentVariableBinder(paramBinder.WrappedBinder);

         return Expression.Block(bodyLocals, bodyStmt);
      }

      private Tuple<String, String>[] _ParseParameters(Parsing.TemplateParserContext parserContext, String input, out String name)
      {
         Contract.Requires(parserContext != null);
         Contract.Requires(input != null);

         var idx = input.IndexOf('(');
         if (idx < 0)
         {
            name = input;
            return null;
         }

         var lidx = input.IndexOf(')', idx + 1);
         if (lidx < 0) throw parserContext.SyntaxError(idx, "Missing ')'.");

         if (lidx < input.Length - 1 && input.Substring(lidx + 1).Trim().Length != 0)
            throw parserContext.SyntaxError(idx, "Expected end of parameter input");

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
         Contract.Requires(argStr != null);

         var spaceIdx = argStr.IndexOfWhiteSpace();
         if (spaceIdx > 0 && spaceIdx < argStr.Length - 1)
            return Tuple.Create(argStr.Substring(0, spaceIdx).Trim(), argStr.Substring(spaceIdx + 1).Trim());
         else
            return Tuple.Create("System.String", argStr); // default type is string
      }

      protected virtual IDefineVariableBinder CreateVariableBinder(Parsing.TemplateParserContext parserContext)
      {
         return new _ParameterBinder();
      }

      protected virtual Expression MakeSetVariableExpr(TemplateParserContext parserContext, Expression context, IDefineVariableBinder binder, String name, Int32 relativePosInSource, Expression @value, List<ParameterExpression> locals)
      {
         Contract.Requires(parserContext != null);
         Contract.Requires(context != null);
         Contract.Requires(binder != null);
         Contract.Requires(name != null);
         Contract.Requires(@value != null);

         return parserContext.SetVariable(context, name, relativePosInSource, @value);
      }

      protected interface IDefineVariableBinder : IVariableBinder
      {
         Dictionary<String, ParameterExpression> ParamDecls { get; set; }
         IVariableBinder WrappedBinder { get; set; }
      }

      /// <summary>
      /// Default variable binder making parameters available as variables.
      /// </summary>
      private class _ParameterBinder : IDefineVariableBinder
      {
         public Dictionary<String, ParameterExpression> ParamDecls { get; set; }
         public IVariableBinder WrappedBinder { get; set; }

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
               throw SyntaxException.Create(null, null, "Cannot set variable '{0}' because it has been bound to a parameter.", name);

            return WrappedBinder == null ? null : WrappedBinder.BindVariableWrite(currentContext, name, value);
         }

         public Expression UnbindVariable(Expression currentContext, String name)
         {
            if (ParamDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot unset variable '{0}' because it has been bound to a parameter.", name);

            return WrappedBinder == null ? null : WrappedBinder.UnbindVariable(currentContext, name);
         }
      }
   }
}
