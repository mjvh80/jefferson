using Jefferson.Output;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public class LetDirective : IDirective
   {
      public String Name
      {
         get { return "let"; }
      }

      public String[] ReservedWords
      {
         get { return new[] { "out" }; }
      }

      public Boolean MayBeEmpty
      {
         get { return false; }
      }

      private static readonly Char[] _sVarSplit = new[] { ';' };
      private static readonly Char[] _sNameValueSplit = new[] { '=' };

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         // Compile variables.
         var compiledVars = new Dictionary<String, CompiledExpression<Object, Object>>();

         // We support two modes.
         // Mode 1: $$let foo = 'bar'; b = 1$$
         // Mode 2:
         // Supports syntax
         //    $$#let baz$$
         //       Definition of baz
         //    $$#out$$
         //       output
         //    $$/let$$
         var isArgOnly = false;
         for (var startIdx = 0; ; )
         {
            var varSepIdx = arguments.IndexOf(';', startIdx);

            // ; can only be used for multiple key value pair style definitions.
            isArgOnly = isArgOnly || varSepIdx >= 0;

            var bindingLen = (varSepIdx < 0 ? arguments.Length : varSepIdx) - startIdx;
            if (bindingLen == 0) throw parserContext.SyntaxError(startIdx, "No variable bindings found.");

            var binding = arguments.Substring(startIdx, bindingLen).Trim();
            if (binding.Length == 0)
               throw parserContext.SyntaxError(startIdx, "No variable bindings found.");

            var eqIdx = binding.IndexOf('=');
            if (eqIdx < 0)
            {
               if (isArgOnly) throw parserContext.SyntaxError(startIdx, "Invalid variable binding: missing '='.");
            }
            else isArgOnly = true;

            var name = (isArgOnly ? binding.Substring(0, eqIdx) : binding.Substring(0)).Trim();
            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(0, "Variable '{0}' has an invalid name.", name);

            String value;
            if (isArgOnly)
            {
               value = binding.Substring(eqIdx + 1).Trim();
               compiledVars.Add(name, parserContext.CompileExpression<Object>(value));
            }
            else
            {
               // Look for $$#out$$.
               var outIdx = parserContext.FindDirectiveEnd(source, 0, "$$#out$$");
               if (outIdx < 0) throw parserContext.SyntaxError(0, "Missing $$#out$$ statement.");

               value = source.Substring(0, outIdx);
               source = source.Substring(outIdx + "$$#out$$".Length);

               // Value in this case is not an expression, but the result of applying the template.
               var parsedDefinition = parserContext.Parse<Object>(value);

               // Add an expression to get and compile at runtime.
               // Todo: this could perhaps cache, if used more than once.
               // Todo(2): can probably be done more efficiently as we don't need the compiledexpression step, so we have some
               // extra lambda invocation overhead.
               var sb = Expression.Variable(typeof(StringBuilderOutputWriter));
               var ctxParam = Expression.Parameter(typeof(Object));
               compiledVars.Add(name, new CompiledExpression<Object, Object>
               {
                  Ast = Expression.Lambda<Func<Object, Object>>(
                           Expression.Block(new[] { sb },
                              Expression.Assign(sb, Expression.New(typeof(StringBuilderOutputWriter))),
                              Expression.Invoke(parsedDefinition, ctxParam, sb),
                              Expression.Convert(
                                 Expression.Call(sb, Utils.GetMethod<StringBuilderOutputWriter>(s => s.GetOutput())),
                                 typeof(Object))),
                           ctxParam),
                  OutputType = typeof(String)
               });
            }

            if (varSepIdx < 0) break;
            startIdx = varSepIdx + 1;
         }

         // Declare them as ParameterExpressions.
         var declaredVars = new Dictionary<String, ParameterExpression>(compiledVars.Count, parserContext.Options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
         var currentContext = parserContext.GetNthContext(0);

         foreach (var kvp in compiledVars)
         {
            declaredVars.Add(kvp.Key, Expression.Variable(kvp.Value.OutputType, kvp.Key));
         }

         var body = new List<Expression>(compiledVars.Count + 1);

         // Add assignment expressions to the body.
         foreach (var kvp in declaredVars)
         {
            // Note: even though we are casting here, there should be no loss of type safety because the underlying
            // expression has compiled to the correct output type but was upcasted to object (yes, unavoidable box)
            // because we asked for object. Perhaps we could avoid doing this using reflection.
            body.Add(Expression.Assign(kvp.Value,
                                       Expression.Convert(Expression.Invoke(compiledVars[kvp.Key].Ast,
                                                             Expression.Convert(currentContext, typeof(Object))),
                                                          compiledVars[kvp.Key].OutputType)));
         }

         var letBinder = new _LetBinder
         {
            VariableDecls = declaredVars
         };

         letBinder.WrappedBinder = parserContext.ReplaceCurrentVariableBinder(letBinder);

         // Compile the scope in which these variables are bound.
         body.Add(Expression.Invoke(parserContext.Parse<Object>(source), currentContext, parserContext.Output));

         // Reinstate the old binder.
         parserContext.ReplaceCurrentVariableBinder(letBinder.WrappedBinder);

         // Declare the variables to a block, and add the assigment and compiled source expressions.
         return Expression.Block(declaredVars.Values, body);
      }

      private class _LetBinder : IVariableBinder
      {
         public Dictionary<String, ParameterExpression> VariableDecls;
         public IVariableBinder WrappedBinder;

         // Update variable declaration to compile the variable name.
         public Expression BindVariableRead(Expression currentContext, String name)
         {
            ParameterExpression variable;
            if (VariableDecls.TryGetValue(name, out variable))
               return variable;

            return WrappedBinder == null ? null : WrappedBinder.BindVariableRead(currentContext, name);
         }

         public Expression BindVariableWrite(Expression currentContext, String name, Expression value)
         {
            // todo: this error sucks, because it's not clear where in the source this is, we need more context
            if (VariableDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot set variable '{0}' because it has been bound in a let context.", name);

            return WrappedBinder == null ? null : WrappedBinder.BindVariableWrite(currentContext, name, value);
         }

         public Expression UnbindVariable(Expression currentContext, String name)
         {
            if (VariableDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot unset variable '{0}' because it has been bound in a let context.", name);

            return WrappedBinder == null ? null : WrappedBinder.UnbindVariable(currentContext, name);
         }
      }
   }
}
