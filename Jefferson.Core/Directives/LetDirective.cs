using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public sealed class LetDirective : DefineDirective
   {
      public LetDirective() : base("let", allowOut: true, requireOut: true) { }

      protected override IDefineVariableBinder CreateVariableBinder(Parsing.TemplateParserContext parserContext)
      {
         return new _LetBinder() { VariableDecls = new Dictionary<String, ParameterExpression>(parserContext.Options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal) };
      }

      protected override Expression MakeSetVariableExpr(Parsing.TemplateParserContext parserContext, Expression context, IDefineVariableBinder binder, String name, Int32 relativePosInSource, Expression value, List<ParameterExpression> locals)
      {
         var var = Expression.Variable(value.Type, name);
         ((_LetBinder)binder).VariableDecls.Add(name, var);
         locals.Add(var);
         return Expression.Assign(var, value);
      }

      private class _LetBinder : IDefineVariableBinder
      {
         // The names being bound.
         public Dictionary<String, ParameterExpression> VariableDecls { get; set; }

         // Parameters used in bindings, if names are parameterized (functions).
         public Dictionary<String, ParameterExpression> ParamDecls { get; set; }

         public IVariableBinder WrappedBinder { get; set; }

         // Update variable declaration to compile the variable name.
         public Expression BindVariableRead(Expression currentContext, String name)
         {
            ParameterExpression variable;
            if (VariableDecls.TryGetValue(name, out variable) || ParamDecls.TryGetValue(name, out variable))
               return variable;

            return WrappedBinder == null ? null : WrappedBinder.BindVariableRead(currentContext, name);
         }

         public Expression BindVariableWrite(Expression currentContext, String name, Expression value)
         {
            // todo: this error sucks, because it's not clear where in the source this is, we need more context
            if (VariableDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot set variable '{0}' because it has been bound in a let context.", name);

            if (ParamDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot set variable '{0}' because it is the name of a let parameter.", name);

            return WrappedBinder == null ? null : WrappedBinder.BindVariableWrite(currentContext, name, value);
         }

         public Expression UnbindVariable(Expression currentContext, String name)
         {
            if (ParamDecls.ContainsKey(name) || VariableDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot unset variable '{0}' because it has been bound in a let context.", name);

            return WrappedBinder == null ? null : WrappedBinder.UnbindVariable(currentContext, name);
         }
      }
   }
}
