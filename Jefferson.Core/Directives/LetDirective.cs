using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
#if true
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
         var variables = arguments.Split(_sVarSplit, StringSplitOptions.RemoveEmptyEntries);

         // Compile variables.
         var compiledVars = new Dictionary<String, CompiledExpression<Object, Object>>(variables.Length);
         foreach (var var in variables)
         {
            var pair = var.Split(_sNameValueSplit, StringSplitOptions.RemoveEmptyEntries);
            var name = pair[0].Trim();
            var value = pair[1].Trim(); // < make this indexoffirst as = may appear in an expression

            // todo: validate

            compiledVars.Add(name, parserContext.EvaluateExpression<Object>(value, parserContext.ShouldThrow));
         }

         // Declare them as ParameterExpressions.
         var declaredVars = new Dictionary<String, ParameterExpression>(variables.Length);
         var currentContext = parserContext.GetNthContext(0);

         foreach (var kvp in compiledVars)
         {
            declaredVars.Add(kvp.Key, Expression.Variable(kvp.Value.OutputType, kvp.Key));
         }

         var body = new List<Expression>(variables.Length + 1);

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

         var oldBinder = parserContext.ReplaceCurrentVariableBinder(new _LetBinder
         {
            VariableDecls = declaredVars
         }); ;

         // Compile the scope in which these variables are bound.
         body.Add(Expression.Invoke(parserContext.Parse<Object>(source), currentContext, parserContext.Output));

         // Reinstate the old binder.
         parserContext.ReplaceCurrentVariableBinder(oldBinder);

         // Declare the variables to a block, and add the assigment and compiled source expressions.
         return Expression.Block(declaredVars.Values, body);
      }

      private class _LetBinder : IVariableBinder
      {
         public Dictionary<String, ParameterExpression> VariableDecls;

         // Update variable declaration to compile the variable name.
         public Expression BindVariable(Expression currentContext, String name)
         {
            ParameterExpression variable;
            if (VariableDecls.TryGetValue(name, out variable))
               return variable;

            return null;
         }
      }
   }
#endif
}
