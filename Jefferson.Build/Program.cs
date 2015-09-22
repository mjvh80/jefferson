using Jefferson.Binders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Jefferson.Build
{
   /// <summary>
   /// Loads AssemblyXxxAttribute attributes from the assembly as variables Xxx into this context. These
   /// can then be used for replacement.
   /// </summary>
   public class AssemblyInfo : IndexerVariableBinder
   {
      private Dictionary<String, Object> _mAsmInfos = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);

      public Object this[String key] { get { return _mAsmInfos[key]; } }

      public AssemblyInfo() : base(new Dictionary<String, Type>(StringComparer.OrdinalIgnoreCase)) { }

      public static AssemblyInfo FromAsm(Assembly asm)
      {
         var result = new AssemblyInfo();
         result.AddAttributes(asm);
         result.OptAddVariable("Version", asm.GetName().Version);
         return result;
      }

      public void OptAddVariable(String name, Object value, Type type = null)
      {
         if (_mAsmInfos.ContainsKey(name)) return;
         AddVariable(name, value, type);
      }

      public void AddVariable(String name, Object value, Type type = null)
      {
         type = type ?? value.GetType();

         Console.WriteLine(" -- adding variable {0} = {1} (type {2})", name, value, type.Name);
         mTypeDeclarations.Add(name, type);
         _mAsmInfos.Add(name, value);
      }

      public void AddAttributes(Assembly asm)
      {
         Console.WriteLine("Binding variables from assembly {0} (at {1})", asm.FullName, asm.Location);

         foreach (var attribute in asm.GetCustomAttributesData())
         {
            var name = attribute.AttributeType.Name;

            if (!name.StartsWith("Assembly") || !attribute.ConstructorArguments.Any())
            {
               Console.WriteLine(" -- skipped: {0}", name);
               continue;
            }

            name = name.Substring("Assembly".Length);

            var idx = name.IndexOf("Attribute");
            if (idx > 0)
               name = name.Substring(0, idx);

            AddVariable(name, attribute.ConstructorArguments[0].Value, attribute.ConstructorArguments[0].ArgumentType);
         }

         Console.WriteLine(" -- done");
      }
   }

   class Program
   {
      private static void _Error(String msg, params Object[] args)
      {
         Console.Error.WriteLine(msg, args);
         Environment.Exit(1);
      }

      private static void Main(String[] args)
      {
         if (args.Length < 3) _Error("Not enough arguments.");

         var asmFile = args[0];

         Assembly asm = null;
         try
         {
            asm = Assembly.ReflectionOnlyLoadFrom(args[0]);
         }
         catch (Exception e)
         {
            _Error("Failed to load assembly {0}: {1}", asmFile, e.Message); throw;
         }

         var info = AssemblyInfo.FromAsm(asm);

         foreach (var pair in args.Skip(1).Zip(args.Skip(2), (source, result) => new { Source = source, Target = result }))
         {
            var source = pair.Source;
            var target = pair.Target;

            if (!File.Exists(source)) _Error("Source file '{0}' not found.", source);

            try
            {
               // note we don't need file scoping as our context is read-only
               Console.WriteLine("Replacing variables in source file {0} to target file {1}.", source, target);
               File.WriteAllText(target, new TemplateParser().ReplaceDeep(File.ReadAllText(source), info));
               Console.WriteLine(" -- done");
            }
            catch (Exception e)
            {
               _Error("Failed to replace variables in target file '{0}': {1}", target, e.Message); throw;
            }
         }
      }
   }
}
