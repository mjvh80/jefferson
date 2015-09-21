using System;
using System.Collections.Generic;
using System.Linq;

namespace Jefferson.FileProcessing
{
   public class VariableScope<TKey, TValue> : Dictionary<TKey, TValue>
   {
      public VariableScope(IEqualityComparer<TKey> comparer) : base(comparer) { }
      public VariableScope(IDictionary<TKey, TValue> values) : base(values) { }
      public VariableScope(IDictionary<TKey, TValue> values, IEqualityComparer<TKey> comparer) : base(values, comparer) { }
      public VariableScope() : base() { }

      public VariableScope<TKey, TValue> ParentScope { get; private set; }

      public VariableScope<TKey, TValue> RootScope
      {
         get
         {
            VariableScope<TKey, TValue> currentScope;
            for (currentScope = this; currentScope.ParentScope != null; currentScope = currentScope.ParentScope) ;
            return currentScope;
         }
      }

      public Boolean IsInScope(TKey key)
      {
         return ContainsKey(key) || (ParentScope != null && ParentScope.IsInScope(key));
      }

      public Boolean TryGetValueInScope(TKey key, out TValue value)
      {
         for (var currentScope = this; currentScope != null; currentScope = currentScope.ParentScope)
            if (currentScope.TryGetValue(key, out value)) return true;
         value = default(TValue);
         return false;
      }

      public IEnumerable<TKey> AllKeysInScope
      {
         get
         {
            return this.Keys.Concat(ParentScope == null ? Enumerable.Empty<TKey>() : ParentScope.AllKeysInScope);
         }
      }

      public VariableScope<TKey, TValue> OpenChildScope()
      {
         return new VariableScope<TKey, TValue>(this.Comparer)
         {
            ParentScope = this
         };
      }
   }
}
