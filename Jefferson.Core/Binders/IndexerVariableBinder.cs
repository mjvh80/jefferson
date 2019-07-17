using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Jefferson.Binders
{
   /// <summary>
   /// Binds variables by looking up an indexer in the current context.
   /// If inheriting, make sure to ensure that variables added to any dictionaries (say)
   /// are declared in the type declarations so that it is known at compile time a variable exists.
   /// </summary>
   public class IndexerVariableBinder : IVariableBinder
   {
      /// <summary>
      /// Map variable names to their types.
      /// </summary>
      protected readonly IDictionary<String, Type> mTypeDeclarations;

      public IndexerVariableBinder(IDictionary<String, Type> variableTypeDeclarations)
      {
         if (variableTypeDeclarations == null)
         {
             throw new ArgumentNullException(nameof(variableTypeDeclarations), "Contract assertion not met: variableTypeDeclarations != null");
         }
         mTypeDeclarations = variableTypeDeclarations;
      }

      private PropertyInfo _GetIndexer(Expression currentContext)
      {
         if (currentContext == null)
         {
             throw new ArgumentNullException(nameof(currentContext), "Contract assertion not met: currentContext != null");
         }

         // Todo: we could look for e.g. things like Chars... i.e. the correctly marked indexer.
         var indexer = currentContext.Type.GetProperty("Item");
         if (indexer == null)
            throw new Exception(String.Format("Context type '{0}' declares variables but provides no indexer to obtain them.", currentContext.Type.FullName));
         return indexer;
      }

      public virtual Expression BindVariableRead(Expression currentContext, String name)
      {
         Type varType;
         if (!mTypeDeclarations.TryGetValue(name, out varType)) return null;

         var indexer = _GetIndexer(currentContext);
         return Expression.Convert(Expression.MakeIndex(currentContext, indexer, new[] { Expression.Constant(name) }), varType);
      }

      public virtual Expression BindVariableWrite(Expression currentContext, String name, Expression value)
      {
         mTypeDeclarations[name] = value.Type;

         var indexer = _GetIndexer(currentContext);
         var indexExpr = Expression.Property(currentContext, indexer, new[] { Expression.Constant(name) });
         return Expression.Assign(indexExpr, Expression.Convert(value, indexExpr.Type));
      }

      public virtual Expression UnbindVariable(Expression currentContext, String name)
      {
         // todo: error incorrect
         if (!mTypeDeclarations.ContainsKey(name))
            throw new InvalidOperationException(String.Format("Variable '{0}' cannot be unset because it has not been set.", name));

         mTypeDeclarations.Remove(name);
         return Expression.Constant("");
      }
   }
}
