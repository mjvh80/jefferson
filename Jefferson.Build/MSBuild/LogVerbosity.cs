using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jefferson.Build.MSBuild
{
    public enum LogVerbosity
    {
        // note: keep ordered from low > high
        Quiet,
        Minimal,
        Normal,
        Detailed,
        Diagnostic
    }
}
