using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Jefferson.Binders
{
    internal static class TypeMapUtils
    {
        private class _DictionaryBackedTypeMap : IVariableTypeMap
        {
            private readonly IDictionary<String, Type> _mMapping;

            public _DictionaryBackedTypeMap(IDictionary<String, Type> mapping)
            {
                _mMapping = mapping;
            }

            public Type TryGetType(String name)
            {
                Type result;
                return _mMapping.TryGetValue(name, out result) ? result : null;
            }

            public void Remove(String name)
            {
                _mMapping.Remove(name);
            }

            public Boolean IsDefined(String name)
            {
                return _mMapping.ContainsKey(name);
            }

            public void DefineType(String name, Type type)
            {
                _mMapping[name] = type; // todo: should we check it exists? 
            }
        }

        public static IVariableTypeMap FromDictionary(IDictionary<String, Type> map)
        {
            Contract.Requires(map != null);
            return new _DictionaryBackedTypeMap(map);
        }

        private class _FuncBackedTypeMap : IVariableTypeMap
        {
            private readonly Func<String, Type> _mMapping;
            private readonly IVariableTypeMap _mDefines;

            public _FuncBackedTypeMap(Func<String, Type> map)
            {
                _mMapping = map;
                _mDefines = TypeMapUtils.FromDictionary(new Dictionary<String, Type>()); // todo: case sensitivity
            }

            public Type TryGetType(String name)
            {
                return _mMapping(name) ?? _mDefines.TryGetType(name);
            }

            public void Remove(String name)
            {
                // todo: quick and dirty for now
                // this will fail with key not found if e.g. the mapping did exist in the func
                // so we should at least improve the error
                _mDefines.Remove(name);
            }

            public Boolean IsDefined(String name)
            {
                return _mMapping(name) != null || _mDefines.IsDefined(name);
            }

            public void DefineType(String name, Type type)
            {
                _mDefines.DefineType(name, type);
            }
        }

        public static IVariableTypeMap FromFunc(Func<String, Type> map)
        {
            Contract.Requires(map != null);
            return new _FuncBackedTypeMap(map);
        }
    }
}
