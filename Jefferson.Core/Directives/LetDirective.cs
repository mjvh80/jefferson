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
         get { return null; }
      }

      private static readonly Char[] _sVarSplit = new[] { ';' };
      private static readonly Char[] _sNameValueSplit = new[] { '=' };

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         // Compile variables.
         var compiledVars = new Dictionary<String, CompiledExpression<Object, Object>>();

         for (var startIdx = 0; ; )
         {
            var varSepIdx = arguments.IndexOf(';', startIdx);

            var bindingLen = (varSepIdx < 0 ? arguments.Length : varSepIdx) - startIdx;
            if (bindingLen == 0) throw parserContext.SyntaxError(startIdx, "Invalid variable binding found: empty.");

            var binding = arguments.Substring(startIdx, bindingLen);

            var eqIdx = binding.IndexOf('=');
            if (eqIdx < 0) throw parserContext.SyntaxError(startIdx, "Invalid variable binding: missing '='.");

            var name = binding.Substring(0, eqIdx).Trim();
            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(0, "Variable '{0}' has an invalid name.", name);

            var value = binding.Substring(eqIdx + 1).Trim();

            compiledVars.Add(name, parserContext.CompileExpression<Object>(value));

            if (varSepIdx < 0) break;
            startIdx = varSepIdx + 1;
         }

         // Declare them as ParameterExpressions.
         var declaredVars = new Dictionary<String, ParameterExpression>(compiledVars.Count);
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
         public Expression BindVariable(Expression currentContext, String name)
         {
            ParameterExpression variable;
            if (VariableDecls.TryGetValue(name, out variable))
               return variable;

            return WrappedBinder.BindVariable(currentContext, name);
         }
      }
   }
}
