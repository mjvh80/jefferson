using System;

namespace Jefferson.Build.MSBuild
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    internal class CommandLineSettableAttribute : Attribute
    {
        // Nothing here, this is a marker only.
    }
}
