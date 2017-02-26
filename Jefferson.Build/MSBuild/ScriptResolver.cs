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
            var isOptional = path.StartsWith("?");

            if (isOptional && path.Length > 1)
                path = path.Substring(1);

            if (Path.IsPathRooted(path))
            {
                _mTask._LogDiagnostic($"Path {path} referenced from {baseFilePath} resolved as {path}");
                return isOptional ? $"?{path}" : path;
            }

            var parentDirectory = Path.GetFullPath(Path.Combine(baseFilePath, ".."));
            var result = Path.Combine(parentDirectory, path);
            _mTask._LogDiagnostic($"Path {(isOptional ? "?" : "")}{path} referenced from {baseFilePath} resolved to {result}.");
            return isOptional ? $"?{result}" : result;
        }

        public override Stream OpenRead(String resolvedPath)
        {
            if (resolvedPath.StartsWith("?") && resolvedPath.Length > 1)
            {
                var file = resolvedPath.Substring(1);
                if (File.Exists(file)) return File.OpenRead(file);

                // File not found, return empty.
                _mTask._Log($"Optional include file {resolvedPath} not found. Ignoring.");
                return new MemoryStream(0);
            }

            return File.OpenRead(resolvedPath);
        }
    }
}
