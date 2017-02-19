using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jefferson.Build.MSBuild
{
    public class ScriptGlobals
    {
        public NameValueCollection MSBuild { get; internal set; }
    }
}
