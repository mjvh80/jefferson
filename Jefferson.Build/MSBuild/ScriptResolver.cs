using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Jefferson.Build.MSBuild
{
    class ScriptResolver : SourceReferenceResolver
    {
        private readonly String _mSolutionDirectory;

        public ScriptResolver(String solutionDirectory)
        {
            _mSolutionDirectory = solutionDirectory;
        }

        public override Boolean Equals(Object other)
            => (other as ScriptResolver)?._mSolutionDirectory == _mSolutionDirectory;

        public override Int32 GetHashCode() => _mSolutionDirectory.GetHashCode();

        public override String NormalizePath(String path, String baseFilePath)
        {
            return null; // todo?
        }

        public override String ResolveReference(String path, String baseFilePath)
        {
            if (path == "<solution>")
                return "<solution>";

            return null;
        }

        public override Stream OpenRead(String resolvedPath)
        {
            if (resolvedPath == "<solution>")
                return File.OpenRead(@"C:\workspace\jefferson\test.csx");
            return null;
        }
    }
}
