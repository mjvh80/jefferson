using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jefferson.FileProcessing;
using Microsoft.CodeAnalysis;

namespace Jefferson.Build.MSBuild
{
    class ScriptResolver : SourceReferenceResolver
    {
        private readonly PreprocessorTask _mTask;
        private readonly SimpleFileProcessor<TemplateContext> _mFileProcessor;

        public ScriptResolver(PreprocessorTask task, SimpleFileProcessor<TemplateContext> fileProcessor)
        {
            _mTask = task;
            _mFileProcessor = fileProcessor;
        }

        public override Boolean Equals(Object other)
            => (other as ScriptResolver)?._mFileProcessor == this._mFileProcessor;

        public override Int32 GetHashCode() => _mFileProcessor.GetHashCode();

        public override String NormalizePath(String path, String baseFilePath)
        {
            return null;
        }

        public override String ResolveReference(String path, String baseFilePath)
        {
            if (Path.IsPathRooted(path))
            {
                _mTask._LogDiagnostic($"Path {path} referenced from {baseFilePath} resolved as {path}");
                return path;
            }

            var parentDirectory = Path.GetFullPath(Path.Combine(baseFilePath, ".."));
            var result = Path.Combine(parentDirectory, path);
            _mTask._LogDiagnostic($"Path {path} referenced from {baseFilePath} resolved to {result}.");
            return result;
        }

        public override Stream OpenRead(String resolvedPath)
        {
            return File.OpenRead(resolvedPath);
        }
    }
}
