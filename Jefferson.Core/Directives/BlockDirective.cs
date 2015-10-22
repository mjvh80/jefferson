using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   /// <summary>
   /// Introduces a block scope. Variables that are e.g. #defined remain within the scope until this directive ends.
   /// Further, it also allows one to access the outer context as follows:
   /// $$#block$$
   ///   $$#let x = 'blah'$$
   ///   $$ $1.x $$
   /// $$/block$$
   /// It would otherwise be impossible to access x in the current scope as let does not introduce a new scope.
   /// </summary>
   public sealed class BlockDirective : IDirective
   {
      private readonly Boolean _mEnableUnbindOutsideOfBlock;

      public BlockDirective(Boolean enableUnbindOutsideOfBlock = true) { _mEnableUnbindOutsideOfBlock = enableUnbindOutsideOfBlock; }

      public String Name
      {
         get { return "block"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         if (source == null) throw parserContext.SyntaxError(0, "#block directive should not be empty"); // could allow nop?
         if (!String.IsNullOrWhiteSpace(arguments)) throw parserContext.SyntaxError(0, "#block directive does not accept arguments");

         var currentContext = parserContext.GetNthContext(0);
         var currentContextAsObj = Expression.Convert(currentContext, typeof(Object));
         var binder = new _BlockScopeBinder()
         {
            VariableDecls = new Dictionary<String, Expression>(parserContext.Options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal),
            WrappedBinder = _mEnableUnbindOutsideOfBlock ? parserContext.CurrentVariableDeclaration : null
         };
         parserContext.PushScope(currentContext.Type, binder);
         var result = Expression.Block(
                         Expression.Call(parserContext.RuntimeContexts, Utils.GetMethod<List<Object>>(l => l.Add(null)), currentContextAsObj),
                         Expression.Invoke(parserContext.Parse<Object>(source), currentContextAsObj, parserContext.Output),
                         Expression.Call(parserContext.RuntimeContexts, Utils.GetMethod<List<Object>>(l => l.RemoveAt(0)),
                            Expression.Subtract(Expression.Property(parserContext.RuntimeContexts, "Count"), Expression.Constant(1))));
         parserContext.PopScope();
         return result;
      }

      private class _BlockScopeBinder : IVariableBinder
      {
         public Dictionary<String, Expression> VariableDecls;
         public IVariableBinder WrappedBinder;

         // Update variable declaration to compile the variable name.
         public Expression BindVariableRead(Expression currentContext, String name)
         {
            if (VariableDecls.ContainsKey(name))
               return VariableDecls[name];

            // We don't need any wrapped binder check here because default resolution logic will walk up the scope tree.
            return null;
         }

         public Expression BindVariableWrite(Expression currentContext, String name, Expression value)
         {
            VariableDecls[name] = value;
            return Expression.Default(value.Type); // note that the result of the assignment is never used
         }

         public Expression UnbindVariable(Expression currentContext, String name)
         {
            if (VariableDecls.ContainsKey(name))
            {
               VariableDecls.Remove(name);
               return Utils.NopExpression;
            }
            else
               return WrappedBinder == null ? null : WrappedBinder.UnbindVariable(currentContext, name);
         }
      }
   }
}
