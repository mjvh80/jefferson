using System;

namespace Jefferson.Binders
{
    /// <summary>
    /// Interface used by the IndexerVariableBinder class to map variable names to types.
    /// </summary>
    public interface IVariableTypeMap
    {
        Type TryGetType(String name);
        void Remove(String name);
        void DefineType(String name, Type type);
        Boolean IsDefined(String name);
    }
}
