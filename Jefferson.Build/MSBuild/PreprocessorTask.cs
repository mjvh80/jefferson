using Jefferson.Build.Directives;
using Jefferson.FileProcessing;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Jefferson.Build.MSBuild
{
    // WARNING: this code is not stable and will change
    public class PreprocessorTask : MarshalByRefObject, ITask
    {
        // WARNING: don't marshal
        [Required]
        public ITaskItem[] Input { get; set; }

        public String[] InputFiles => Input.Select(item => item.ToString()).ToArray();

        [Output]
        public String[] ProcessedFiles { get; protected set; }

        [Output]
        public String[] OriginalFiles { get; protected set; }

        public String[] References { get; set; }

        public String ScriptFile { get; set; }

        private readonly AppDomain _mExecutionDomain;

        // WARNING: not accessible in our appdomain due to MSBuild assembly binding redirects.
        // Our AppDomain only employs our own app.config which is used to redirect binding for Roslyn
        // and other assemblies.
        // However, we reference MSBuild assemblies v4 but these need to get redirected to the *actual* MSBuild
        // assembly loaded into the MSBuild AppDomain.
        public IBuildEngine BuildEngine { get; set; }

        // Warning: do not Marshal (see BuildEngine)
        public ITaskHost HostObject { get; set; }

        protected TaskLoggingHelper Log { get; set; }

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
        }

        private AssemblyName[] _GetMSBuildAssemblies()
            => AppDomain.CurrentDomain.GetAssemblies()
                .Where(asm => asm.FullName.StartsWith("Microsoft.Build"))
                .Select(asm => asm.GetName())
                .ToArray();

        // todo: remove
        private class _LoadMSBuildAssembliesIntoDomain : MarshalByRefObject
        {
            public void LoadAssembliesFrom(PreprocessorTask task)
            {
                Console.WriteLine("LOADING");

                foreach (var asm in task._GetMSBuildAssemblies())
                {
                    Console.WriteLine("LOAD: " + asm);
                    AppDomain.CurrentDomain.Load(asm);
                }

                Console.WriteLine("DONE: " + task._GetMSBuildAssemblies().Length);
            }
        }


        public Boolean Execute()
        {
            Log.LogMessage($"Got {_GetMSBuildAssemblies().Length} MSBuild asms...");




            var runner = ((_ExecuteInDomain)
                    _mExecutionDomain.CreateInstanceAndUnwrap(GetType().Assembly.FullName, typeof(_ExecuteInDomain).FullName));
            runner.task = this;


            //            System.Diagnostics.Debugger.Launch();
            //          System.Diagnostics.Debugger.Break();


            Log.LogMessage("Build engine can be marshalled: " + (this.BuildEngine is MarshalByRefObject));
            Log.LogMessage("Build engine location:  " + this.BuildEngine.GetType().Assembly.Location);

            //Log.LogMessage("Loaded assemblies are ");
            //foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            //    if (asm.FullName.StartsWith("Microsoft.Build"))
            //        Log.LogMessage($" - {asm.FullName} from {asm.Location}");

            //Console.WriteLine("NOW GOT : " + _GetMSBuildAssemblies().Length);

            return runner.Execute();
        }

        private class _ExecuteInDomain : MarshalByRefObject
        {
            public PreprocessorTask task;

            public Boolean Execute()
            {
                task.Log.LogMessage(
                    "Starting Jefferson preprocessor. Please see project file for MSBuild configuration.");

                var bet = typeof(IBuildEngine);
                task.Log.LogMessage("BuildEngine loaded from " + bet.Assembly.Location);

                IEnumerable<ScriptVariable> scriptVars = null;
                if (task.ScriptFile != null)
                {
                    task.Log.LogMessage(Path.GetFullPath(".\\" + task.ScriptFile));
                    task.Log.LogMessage("Current dir = " + Path.GetFullPath("."));

                    if (!File.Exists(task.ScriptFile))
                        throw new Exception($"Template script file {task.ScriptFile} not found.");

#if CODEDOM
                var script = File.ReadAllText(task.ScriptFile);

                const String scriptContextName = "ScriptedTemplateContext";

                String exeName = String.Format(@"{0}\{1}.dll",
                    System.Environment.CurrentDirectory,
                    Path.GetFileName(task.ScriptFile).Replace(".", "_"));

                var source = $@"
                    using Microsoft.Build.Framework;
                    using Microsoft.Build.Utilities;
                    using System;
 // todo: usings
                    public class {scriptContextName}: {typeof(TemplateContext).FullName.Replace('+', '.')} {{
                       public {scriptContextName}(IBuildEngine engine, TaskLoggingHelper logger): 
                          base(engine, logger) {{}}

                    {script}

                    }}";

                task.Log.LogMessage("Compiling source: \r\n" + source);

                task.Log.LogMessage("AppDomain base is: " + AppDomain.CurrentDomain.BaseDirectory);

                // todo: references
                //      var refs = AppDomain.CurrentDomain.GetAssemblies().Select(asm => asm.Location).ToArray();

                //var refs =
                //    AppDomain.CurrentDomain.GetAssemblies()
                //        .Select(asm => asm.GlobalAssemblyCache ? asm.FullName : asm.Location)
                //        .Concat(new[] { "mscorlib" })
                //        .ToArray();

                var refs = new[]
                {

                        typeof(Task).Assembly.FullName,
                        GetType().Assembly.Location,
                    };

                var result = provider.CompileAssemblyFromSource(new CompilerParameters(refs)
                {
                    GenerateInMemory = false,
                    CompilerOptions = "/langversion:5",
                    OutputAssembly = exeName
                    // todo: warning levels?

                }, source);

                if (result.Errors.Count > 0)
                {
                    foreach (CompilerError error in result.Errors)
                    {
                        task.Log.LogError(
                            $"Script errors in file {error.FileName} at line {error.Line}: {error.ErrorText}");
                    }

                    throw new Exception($"There were errors in the template script {task.ScriptFile}.");
                }

                contextType = result.CompiledAssembly.GetType(scriptContextName);
#endif

                    // todo: options
                    var script = CSharpScript.Create(File.ReadAllText(task.ScriptFile), ScriptOptions.Default);




                    task.Log.LogMessage("Location of DLL: " + script.GetType().Assembly.Location);
                    task.Log.LogMessage("File version: " + script.GetType().Assembly.FullName);


                    task.Log.LogMessage(AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString());

                    task.Log.LogMessage("Loaded assemblies are ");
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        task.Log.LogMessage($" - {asm.FullName} from {asm.Location}");



                    //   throw new Exception("fuuuck");

                    var compilation = script.Compile(CancellationToken.None);

                    //      var compilation = (IEnumerable<Diagnostic>)compileMethod.Invoke(script, new object[] { CancellationToken.None });



                    // Todo: warningsasoerrors?
                    foreach (var diagnostic in compilation)
                        if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                            throw new Exception($"Error in script {task.ScriptFile}: {diagnostic}"); // todo

                    // todo: pass msbuild properties as globals to the script
                    var run = script.RunAsync(null).Result; // block

                    scriptVars = run.Variables;
                }

                var contextInstance = new TemplateContext(scriptVars, task);

                var fileProcessor = new SimpleFileProcessor<TemplateContext>(contextInstance,
                    TemplateParser.GetDefaultDirectives().Concat(new[] { new ProcessDirective() }).ToArray());

                var processedFiles = new List<String>();
                var originalFiles = new List<String>();



                var skipCount = 0;
                foreach (var file in task.InputFiles)
                {
                    var firstLine = File.ReadLines(file).FirstOrDefault();


                    // todo: we could allow the script to filter source?
                    // by providing some sort of fixed named method, or class maybe?
                    // or use the script value

                    // We could perhaps look for any $$ in the entire contents but I worry about performance
                    // or whether that's really safe to do.
                    if (firstLine?.Trim().StartsWith("//$$#process") == true)
                    {
                        task.Log.LogMessage($"Processing replacements in file {file}..");

                        String result = null;
                        try
                        {
                            result = fileProcessor.Replace(File.ReadAllText(file), deep: false);
                        }
                        catch (Exception e)
                        {
                            // File might not be a Jefferson file.
                            //      task.Log.LogError("Error replacing variables, file skipped.");
                            //      task._LogException(e);


                            // exception should not cross domains here!

                            throw new Exception(e.InnerException.Message);


                            //          throw; // todo

                            skipCount += 1;
                        }

                        if (result == null) continue;

                        var tempFile = Path.GetTempFileName() + "_" + Path.GetFileName(file);

                        File.WriteAllText(tempFile, result);
                        processedFiles.Add(tempFile);
                        originalFiles.Add(file);
                    }
                }

                task.Log.LogMessage(
                    $"Done processing files. Processed {processedFiles.Count} files successfully, skipped {skipCount} file{(skipCount == 1 ? "s" : "")} due to errors.");

                task.ProcessedFiles = processedFiles.ToArray();
                task.OriginalFiles = originalFiles.ToArray();

                return true;
            }

        }

        private void _LogException(Exception e)
        {
            if (e == null) return;
            Log.LogErrorFromException(e);
            _LogException(e.InnerException);
        }

        private KeyValuePair<String, String>[] _GetAllEnvironmentVariables()
        {
            return this.BuildEngine.GetAllEnvironmentVariables()
                .Select(tuple => new KeyValuePair<String, String>(tuple.Item1, String.Join(";", tuple.Item2)))
                .ToArray();
        }

        // TODO: add warning here
        public class TemplateContext : FileScopeContext<TemplateContext, SimpleFileProcessor<TemplateContext>>
        {
            public TemplateContext(IEnumerable<ScriptVariable> variables, PreprocessorTask task)
            {
                // Make all of the builds properties and items available to formatting.

                // todo: decide what the best order is: MSBuild props over script or other way round.
                foreach (var pair in task._GetAllEnvironmentVariables())
                    try
                    {
                        KeyValueStore.Add(pair.Key, pair.Value);


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
