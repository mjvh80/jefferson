using System;
using System.Collections.Generic;
using System.Linq;

namespace Jefferson.FileProcessing
{
   public class VariableScope<TKey, TValue> : IDictionary<TKey, TValue>
   {
      private readonly IEqualityComparer<TKey> _mComparer;
      private readonly Dictionary<TKey, TValue> _mDict;
      internal readonly Dictionary<TKey, Type> KnownNames;
      private readonly Func<TValue, Type> _mGetType;

      public VariableScope(IEqualityComparer<TKey> comparer, Func<TValue, Type> getType = null)
      {
         _mComparer = comparer;
         KnownNames = new Dictionary<TKey, Type>(comparer);
         _mDict = new Dictionary<TKey, TValue>(comparer);
         _mGetType = getType ?? (v => v.GetType());
      }

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

      public TValue GetValueInScope(TKey key)
      {
         TValue value;
         if (TryGetValueInScope(key, out value))
            return value;
         throw new KeyNotFoundException("Could not find key " + key);
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
         return new VariableScope<TKey, TValue>(this._mComparer)
         {
            ParentScope = this
         };
      }

      public Boolean IsKnownNameInScope(TKey name)
      {
         return this.KnownNames.ContainsKey(name) || (this.ParentScope != null && this.ParentScope.IsKnownNameInScope(name));
      }

      #region Dictionary Implementation

      // This dictionary must keep track of which names are known at compile time.

      public void Add(TKey key, TValue value)
      {
         _mDict.Add(key, value);
         KnownNames.Add(key, _mGetType(value));
      }

      public Boolean ContainsKey(TKey key)
      {
         return _mDict.ContainsKey(key);
      }

      public ICollection<TKey> Keys
      {
         get { return _mDict.Keys; }
      }

      public Boolean Remove(TKey key)
      {
         KnownNames.Remove(key);
         return _mDict.Remove(key);
      }

      public Boolean TryGetValue(TKey key, out TValue value)
      {
         return _mDict.TryGetValue(key, out value);
      }

      public ICollection<TValue> Values
      {
         get { return _mDict.Values; }
      }

      public TValue this[TKey key]
      {
         get
         {
            return _mDict[key];
         }
         set
         {
            _mDict[key] = value;
            KnownNames[key] = _mGetType(value);
         }
      }

      public void Add(KeyValuePair<TKey, TValue> item)
      {
         _mDict.Add(item.Key, item.Value);
         KnownNames.Add(item.Key, _mGetType(item.Value));
      }

      public void Clear()
      {
         _mDict.Clear();
         KnownNames.Clear();
      }

      public Boolean Contains(KeyValuePair<TKey, TValue> item)
      {
         return _mDict.ContainsKey(item.Key);
      }

      public void CopyTo(KeyValuePair<TKey, TValue>[] array, Int32 arrayIndex)
      {
         ((IDictionary<TKey, TValue>)_mDict).CopyTo(array, arrayIndex);
      }

      public Int32 Count
      {
         get { return _mDict.Count; }
      }

      public Boolean IsReadOnly
      {
         get { return false; }
      }

      public Boolean Remove(KeyValuePair<TKey, TValue> item)
      {
         KnownNames.Remove(item.Key);
         return _mDict.Remove(item.Key);
      }

      public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
      {
         return _mDict.GetEnumerator();
      }

      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

      #endregion
   }
}
