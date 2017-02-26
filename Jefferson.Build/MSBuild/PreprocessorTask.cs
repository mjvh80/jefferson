using Jefferson.Build.Directives;
using Jefferson.FileProcessing;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Jefferson.Build.Extensions;

namespace Jefferson.Build.MSBuild
{
    // WARNING: this code is not stable and will change
    public class PreprocessorTask : MarshalByRefObject, ITask
    {
        /* 
         * Notes on properties:
         * 
         * There are 2 ways to set these, being:
         * 
         * 1. A commandline switch (e.g. /p:JeffersonDebug=true)
         *    a. Jefferson prefixed
         *    b. No Jefferson prefix
         * 2. A setting in the Task in a csproj file.
         * 
         * Commandline switches should always take priority over built in settings.
         * Jefferson prefixed values always take precedence, e.g. DefineConstants would be resolved in order:
         * 1. JeffersonDefineConstants property
         * 2. DefineConstants config property
         * 3. Property set in this file.
         * 
         * Commandline switches should be prefixed with "Jefferson", thus the Debug property
         * corresponds to JeffersonDebug on the command line.
         * 
         * NOTE: not all properties are settable on the commandline. The ones that are
         * are marked with [CommandLineSettable].
         * 
         */

        // WARNING: don't marshal
        [Required]
        public ITaskItem[] Input { get; set; }

        [CommandLineSettable]
        public Boolean Debug { get; set; }

        [CommandLineSettable]
        public String DefineConstants { get; set; }

        public String[] InputFiles => Input.Select(item => item.ToString()).ToArray();

        [Output]
        public String[] ProcessedFiles { get; protected set; }

        [Output]
        public String[] OriginalFiles { get; protected set; }

        public String[] References { get; set; }

        [CommandLineSettable]
        public Boolean RequireSolutionScript { get; set; }

        [CommandLineSettable]
        public String SolutionDirectory { get; set; }

        [CommandLineSettable]
        public String ScriptFile { get; set; }

        private readonly AppDomain _mExecutionDomain;

        public LogVerbosity MSBuildVerbosity { get; }

        [CommandLineSettable]
        public LogVerbosity? LogVerbosityOverride { get; set; }

        // WARNING: not accessible in our appdomain due to MSBuild assembly binding redirects.
        // Our AppDomain only employs our own app.config which is used to redirect binding for Roslyn
        // and other assemblies.
        // However, we reference MSBuild assemblies v4 but these need to get redirected to the *actual* MSBuild
        // assembly loaded into the MSBuild AppDomain.
        public IBuildEngine BuildEngine { get; set; }

        // Warning: do not Marshal (see BuildEngine)
        public ITaskHost HostObject { get; set; }

        protected TaskLoggingHelper Log { get; set; }

        // WARNING: do not use from another domain.
        public T GetProperty<T>(Expression<Func<PreprocessorTask, T>> expr) => PreprocessorTask.GetProperty(this, expr);

        // NOTE: static to avoid having to Marshal the expression tree.
        public static T GetProperty<T>(PreprocessorTask self, Expression<Func<PreprocessorTask, T>> expression)
        {
            var memberExpr = expression.Body as System.Linq.Expressions.MemberExpression;
            if (memberExpr == null) throw new Exception("Invalid expression: expected a member access, got something else.");

            if (memberExpr.Member.GetCustomAttribute<CommandLineSettableAttribute>() == null)
                throw new Exception($"Invalid property {memberExpr.Member.Name}: property not marked as command line settable.");

            var cmdLineName = "Jefferson" + memberExpr.Member.Name;
            var cmdLineValue = String.Join(";", self._GetEnvironmentVariable(cmdLineName));

            if (String.IsNullOrEmpty(cmdLineValue))
            {
                // try without the prefix.
                cmdLineValue = String.Join(";", self._GetEnvironmentVariable(memberExpr.Member.Name));

                if (!String.IsNullOrEmpty(cmdLineValue))
                    self._LogDiagnostic(
                        $"Property '{memberExpr.Member.Name}' resolved to MSBuild property with value '{cmdLineValue}'.");
            }
            else
                self._LogDiagnostic($"Property '{memberExpr.Member.Name}' resolved to MSBuild property 'Jefferson{memberExpr.Member.Name}' with value '{cmdLineValue}'");

            if (!String.IsNullOrEmpty(cmdLineValue))
            {
                // todo: might have to distinguish null/empty to allow *unsetting* of settings?
                self._LogDiagnostic($"Converting value to type {typeof(T)}");
                //  return (T)Convert.ChangeType(cmdLineValue, typeof(T));

                return Utils.ConvertType<T>(cmdLineValue);
            }

            var @value = (T)((PropertyInfo)memberExpr.Member).GetValue(self);
            self._LogDiagnostic($"Property '{memberExpr.Member.Name}' resolved to Task instance property with value {@value}");
            return @value;
        }

        public PreprocessorTask()
        {
            var baseFile = GetType().Assembly.Location;
            var domainSetup = new AppDomainSetup
            {
                // We need our assemblies to load from the same location of Jefferson.Build.
                // In particular we need to avoid loading the Roslyn assembies in the msbuild directory
                // which is the BaseDirectory if running from msbuild.
                ApplicationBase = Path.GetFullPath(Path.Combine(baseFile, "..")),

                // We'll need the app.config to facilitate binding redirects
                ConfigurationFile = Path.GetFullPath(baseFile + ".config"),
            };
            _mExecutionDomain = AppDomain.CreateDomain("JeffersonPrecprocessorDomain", AppDomain.CurrentDomain.Evidence,
                domainSetup);

            Log = new TaskLoggingHelper(this);

            // Work out log verbosity. Bit of a hack.
            // todo: test from VS build
            var cmdLine = Environment.CommandLine;
            if (Regex.IsMatch(cmdLine, "/v:q|/verbosity:q"))
                MSBuildVerbosity = LogVerbosity.Quiet;
            else if (Regex.IsMatch(cmdLine, "/v:m|/verbosity:m"))
                MSBuildVerbosity = LogVerbosity.Minimal;
            else if (Regex.IsMatch(cmdLine, "/v:n|/verbosity:n"))
                MSBuildVerbosity = LogVerbosity.Normal;
            else if (Regex.IsMatch(cmdLine, "/v:di|/verbosity:di"))
                MSBuildVerbosity = LogVerbosity.Diagnostic;
            else if (Regex.IsMatch(cmdLine, "/v:d|/verbosity:d"))
                MSBuildVerbosity = LogVerbosity.Detailed;
            else
                MSBuildVerbosity = LogVerbosity.Normal;
        }

        public Boolean Execute()
        {
            _Log("Starting Jefferson preprocessor. Please see project file for MSBuild configuration.");
            _Log("Detected MSBuild log verbosity: " + MSBuildVerbosity);

            // Create a proxy to do this in our execution domain.
            var runner = ((_ExecuteInDomain)_mExecutionDomain.CreateInstanceAndUnwrap(GetType().Assembly.FullName, typeof(_ExecuteInDomain).FullName));
            runner.Task = this;

            _LogVerbose("Command line is " + System.Environment.CommandLine);

            if (GetProperty(task => task.Debug))
                Debugger.Launch();

            _Log("About to start execution in execution domain.");
            return runner.Execute();
        }

        private class _ExecuteInDomain : MarshalByRefObject
        {
            public PreprocessorTask Task;

            public Boolean Execute()
            {
                Task._Log("Starting Execute");

                var buildVariables = Task._GetAllEnvironmentVariables();

                // Dump if diagnostic logging.
                if (Task.MSBuildVerbosity >= LogVerbosity.Diagnostic)
                {
                    Task._Log("Dumping MSBuild variables:");
                    foreach (var kvp in buildVariables)
                        Task._Log($"{kvp.Key} = {String.Join(";", kvp.Value)}");
                }

                IEnumerable<ScriptVariable> scriptVars = null;
                if (Task.ScriptFile != null)
                {
                    Task._Log("Using script file: " + Task.ScriptFile);
                    Task._LogVerbose("Current directory is " + Path.GetFullPath("."));

                    if (!File.Exists(Task.ScriptFile))
                        throw new Exception($"Template script file {Task.ScriptFile} not found.");

                    // If we have conditional compilation symbols, let's #define these.
                    // Note that the "script" API gives us no way to update the SyntaxTree (e.g.
                    // by injecting directive trivia.
                    var buildVariableMap = _GetNameValueCollection(buildVariables);
                    var rawDefineConstants = GetProperty(Task, task => task.DefineConstants);

                    var defines = rawDefineConstants.Split(';')
                                    .Where(s => !String.IsNullOrWhiteSpace(s))
                                    .Where(s => Regex.IsMatch(@"\w[\w\d]*", s)) // probably too crude
                                    .GroupBy(s => s.Trim())
                                    .Select(g => g.Key)
                                    .ToArray();

                    Task._LogVerbose("Define directives used: " + String.Join(";", defines));

                    var globals = new ScriptGlobals
                    {
                        MSBuild = buildVariableMap
                    };

                    // Create the script by defining preprocessor symbols, loading our solution config file
                    // and resetting line counting.
                    var scriptText =
                        String.Join(Environment.NewLine, defines.Select(define => $"#define {define}")).OptAddNewline() +
                        "#line 1".OptAddNewline() +
                        File.ReadAllText(Task.ScriptFile);

                    Task._LogDiagnostic("Script before processing is");
                    Task._LogDiagnostic(scriptText);

                    // Process the script itself, before executing it.

                    // First, run preprocessor definitions to make these available.
                    var script =
                        CSharpScript.Create(scriptText,
                            ScriptOptions.Default.WithFilePath(Task.ScriptFile)
                                                 .WithSourceResolver(new ScriptResolver(null)),
                            globalsType: globals.GetType());

                    var diagnostics = script.Compile();

                    // Todo: warningsasoerrors? Better yet, infer setting used by project?
                    // todo: improve error messages here
                    // todo: if using defines, check if we need to patch linenumbers
                    var foundError = false;
                    foreach (var diagnostic in diagnostics)
                        if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        {
                            Task._Log(diagnostic.ToString());
                            foundError = true;
                        }

                    if (foundError)
                        throw new Exception($"Error in script {Task.ScriptFile}, see log for details.");

                    // todo: pass msbuild properties as globals to the script
                    var run = script.RunAsync(globals).Result; // block
                    scriptVars = run.Variables;
                }

                var contextInstance = new TemplateContext(scriptVars, buildVariables);

                var fileProcessor = new SimpleFileProcessor<TemplateContext>(contextInstance,
                    TemplateParser.GetDefaultDirectives().Concat(new[] { new ProcessDirective() }).ToArray());

                var processedFiles = new List<String>();
                var originalFiles = new List<String>();

                foreach (var file in Task.InputFiles)
                {
                    if (!File.Exists(file))
                        throw new Exception($"Could not find file '{file}'.");

                    // Get first non-empty line.
                    var firstLine =
                        File.ReadLines(file).FirstOrDefault(line => !String.IsNullOrWhiteSpace(line));

                    // Look for our process marker.
                    // todo: we could allow the script to filter source?
                    // by providing some sort of fixed named method, or class maybe?
                    // or use the script value
                    // We could perhaps look for any $$ in the entire contents but I worry about performance
                    // or whether that's really safe to do.
                    if (firstLine?.Trim().StartsWith("//$$#process") == true)
                    {
                        Task.Log.LogMessage($"Processing replacements in file {file}..");

                        String result = null;
                        try
                        {
                            result = fileProcessor.Replace(File.ReadAllText(file), deep: false);
                        }
                        catch (Exception e)
                        {
                            // It is important that the exception does not cross domains.
                            // Because Jefferson is not loaded in the MSBuild domain.
                            for (var currentError = e; currentError != null; currentError = currentError.InnerException)
                                Task.Log.LogError($"{currentError.Message}\r\n{currentError.StackTrace}");

                            throw new Exception($"File replacement in file '{file}' failed. See build log for details.");
                        }

                        if (result == null) continue;

                        var tempFile = Path.GetTempFileName() + "_" + Path.GetFileName(file);

                        File.WriteAllText(tempFile, result);
                        processedFiles.Add(tempFile);
                        originalFiles.Add(file);
                    }
                    else
                        Task.Log.LogMessage($"Skipped processing for file {file} (no marker found).");
                }

                Task.Log.LogMessage($"Done processing files. Processed {processedFiles.Count} files.");

                Task.ProcessedFiles = processedFiles.ToArray();
                Task.OriginalFiles = originalFiles.ToArray();

                return true;
            }

            private static NameValueCollection _GetNameValueCollection(KeyValuePair<String, String[]>[] map)
            {
                var result = new NameValueCollection();
                foreach (var pair in map)
                    foreach (var @value in pair.Value)
                        result.Add(pair.Key, @value);
                return result;
            }
        }

        // Note: return type must be marshallable.
        private KeyValuePair<String, String[]>[] _GetAllEnvironmentVariables()
            => this.BuildEngine.GetAllEnvironmentVariables()
                .Select(tuple => new KeyValuePair<String, String[]>(tuple.Item1, tuple.Item2.ToArray()))
                .ToArray();

        private String _GetEnvironmentVariable(String name)
            => String.Join(";", this.BuildEngine.GetEnvironmentVariable(name, false));

        private void _Log(String msg) => this.Log.LogMessage(msg);

        private void _LogVerbose(String msg)
        {
            if (_GetLogVerbosity() >= LogVerbosity.Detailed)
                this.Log.LogMessage(msg);
        }

        private void _LogDiagnostic(String msg)
        {
            if (_GetLogVerbosity() >= LogVerbosity.Diagnostic)
                this.Log.LogMessage(msg);
        }

        // Todo: continuous fetching of log verbosity is not ideal but probably not really a problem in this case
        private LogVerbosity _GetLogVerbosity() => GetProperty<LogVerbosity?>(task => task.LogVerbosityOverride) ?? MSBuildVerbosity;

        public class TemplateContext : FileScopeContext<TemplateContext, SimpleFileProcessor<TemplateContext>>
        {
            public TemplateContext(IEnumerable<ScriptVariable> variables, KeyValuePair<String, String[]>[] buildVariables)
            {
                // Make all of the builds properties and items available to formatting.

                // todo: decide what the best order is: MSBuild props over script or other way round.
                foreach (var pair in buildVariables)
                    try
                    {
                        KeyValueStore.Add(pair.Key, String.Join(";", pair.Value));


                    }
                    catch (Exception e)
                    {
                        throw; // todo
                               //logger.LogMessage($"Failed to add key {pair.Item1}");
                               //logger.LogErrorFromException(e);
                    }

                if (variables != null)
                    foreach (var scriptVar in variables)
                    {
                        try
                        {
                            KeyValueStore.Add(scriptVar.Name, scriptVar.Value);
                        }
                        catch (Exception e)
                        {
                            throw;
                            //logger.LogMessage($"Failed to add script variable {scriptVar.Name}.");
                            //logger.LogErrorFromException(e);
                        }
                    }
            }
        }
    }
}
