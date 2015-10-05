using System;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;

namespace Jefferson
{
   /// <summary>
   /// Can be used to declare "dynamic" variables, i.e. variables that do not resolve to properties or fields of the context
   /// instance.
   /// E.g. a file on which Jefferson is used could have a mechanism to declare variables (e.g. in xml:
   /// <![CDATA[
   ///   <variables>
   ///      <variable name="foobar" value="true" isexpr="true" />
   ///   </variables>
   /// ]]>
   /// Nested scopes, such as those created by $$#each$$ may themselves support variable declarations in that scope.
   /// </summary>
   [ContractClass(typeof(Contracts.VariableBinderContract))]
   public interface IVariableBinder
   {
      Expression BindVariableRead(Expression currentContext, String name);

      Expression UnbindVariable(Expression currentContext, String name);

      Expression BindVariableWrite(Expression currentContext, String name, Expression value);
   }

   namespace Contracts
   {
      [ContractClassFor(typeof(IVariableBinder))]
      public abstract class VariableBinderContract : IVariableBinder
      {
         public Expression BindVariableRead(Expression currentContext, String name)
         {
            Contract.Requires(currentContext != null);
            Contract.Requires(name != null);

            // result may be null

            return null;
         }

         public Expression UnbindVariable(Expression currentContext, String name)
         {
            Contract.Requires(currentContext != null);
            Contract.Requires(name != null);

            // result may be null
            return null;
         }

         public Expression BindVariableWrite(Expression currentContext, String name, Expression value)
         {
            Contract.Requires(currentContext != null);
            Contract.Requires(name != null);
            Contract.Requires(value != null);

            // result may be null
            return null;
         }
      }
   }
}
