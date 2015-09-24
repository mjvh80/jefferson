using System;

namespace Jefferson.Binders
{
   public class ReadOnlyBinder : IVariableBinder
   {
      public System.Linq.Expressions.Expression BindVariableRead(System.Linq.Expressions.Expression currentContext, String name)
      {
         return null;
      }

      public System.Linq.Expressions.Expression UnbindVariable(System.Linq.Expressions.Expression currentContext, String name)
      {
         throw new InvalidOperationException(String.Format("Cannot unset variable '{0}' because current binder is read-only."));
      }

      public System.Linq.Expressions.Expression BindVariableWrite(System.Linq.Expressions.Expression currentContext, String name, System.Linq.Expressions.Expression value)
      {
         throw new InvalidOperationException(String.Format("Cannot set variable '{0}' because current binder is read-only.", name));
      }
   }
}
