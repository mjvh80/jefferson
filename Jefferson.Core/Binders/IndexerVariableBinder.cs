using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jefferson.Binders
{
   /// <summary>
   /// Binds variables by looking up an indexer in the current context.
   /// </summary>
   public class IndexerVariableBinder : IVariableBinder
   {
      protected readonly IDictionary<String, Type> mTypeDeclarations;

      public IndexerVariableBinder(IDictionary<String, Type> variableTypeDeclarations)
      {
         Ensure.NotNull(variableTypeDeclarations, "variableTypeDeclarations");
         mTypeDeclarations = variableTypeDeclarations;
      }

      public System.Linq.Expressions.Expression BindVariable(Expression currentContext, String name)
      {
         Type varType;
         if (!mTypeDeclarations.TryGetValue(name, out varType)) return null;

         var indexer = currentContext.Type.GetProperty("Item");
         if (indexer == null)
            throw SyntaxException.Create("Context type '{0}' declares variables but provides no indexer to obtain them.", currentContext.Type.FullName);

         return Expression.Convert(Expression.MakeIndex(currentContext, indexer, new[] { Expression.Constant(name) }), varType);
      }
   }
}
