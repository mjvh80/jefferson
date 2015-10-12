using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;

namespace Jefferson.FileProcessing
{
   public class FileScopeContext<TSelf, TProcessor> : IVariableBinder
      where TSelf : FileScopeContext<TSelf, TProcessor>
      where TProcessor : FileProcessor<TProcessor, TSelf>
   {
      public VariableScope<String, Object> KeyValueStore = new VariableScope<String, Object>(StringComparer.OrdinalIgnoreCase);

      public Boolean AllowUnknownNames = false;

      public TProcessor Processor { get; internal set; }

      public HashSet<String> ReadOnlyVariables = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

      public virtual TSelf CreateChildContext()
      {
         var clone = (TSelf)this.MemberwiseClone();
         clone.KeyValueStore = this.KeyValueStore.OpenChildScope();
         clone.AllowUnknownNames = this.AllowUnknownNames;
         clone.Processor = null; // just to be explicit, must be set by processor
         return clone;
      }

      #region Context Properties and Methods

      public String Trim(String str)
      {
         return str == null ? null : str.Trim();
      }

      /// <summary>
      /// Returns the raw output of a given file. This raw output is not further processed, although variables may be recursively resolved by the varreplacer.
      /// </summary>
      public String IncludeFile(String file)
      {
         Contract.Requires(file != null);
         if (!File.Exists(file)) throw Processor.Error("IncludeFile: file '{0}' does not exist (processed file: '{1}')", file, Processor.GetCurrentFile());
         return File.ReadAllText(file);
      }

      /// <summary>
      /// Returns the raw output of the given file or the empty string if the file does not exist.
      /// </summary>
      public String OptIncludeFile(String file)
      {
         Contract.Requires(file != null);
         if (!File.Exists(file)) return "";
         return File.ReadAllText(file);
      }

      public Boolean DirExists(String dir) { return Directory.Exists(dir); }
      public Boolean FileExists(String file) { return File.Exists(file); }

      public String JoinPath(String dir, String path) { return Path.Combine(dir, path); }

      public String Download(String url)
      {
         Contract.Requires(url != null);
         Contract.Ensures(Contract.Result<String>() != null);

         using (var client = new WebClient())
            return client.DownloadString(url);
      }

      public String MessageBox(String msg)
      {
         var asm = Assembly.Load("System.Windows.Forms, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
         var type = asm.GetType("System.Windows.Forms.MessageBox");
         var method = type.GetMethod("Show", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, new[] { typeof(String) }, null);
         method.Invoke(null, new[] { msg });
         return "";
      }

      public String PID
      {
         get
         {
            return System.Diagnostics.Process.GetCurrentProcess().Id.ToString(NumberFormatInfo.InvariantInfo);
         }
      }

      public String MACHINE
      {
         get
         {
            return System.Environment.MachineName;
         }
      }

      public String Raise(String error)
      {
         Contract.Requires(error != null);
         throw Processor.Error("Error raised from template: " + error);
      }

      public String LaunchDebugger()
      {
         var file = this.Processor.GetCurrentFile();
         System.Diagnostics.Debugger.Launch();
         return null;
      }

      public String BreakDebugger()
      {
         if (System.Diagnostics.Debugger.IsAttached)
         {
            var file = this.Processor.GetCurrentFile();
            System.Diagnostics.Debugger.Break();
         }
         return null;
      }

      #endregion

      #region Xml Processing Support

      protected virtual XmlDocument ProcessXmlFile(String fileName, XmlDocument doc, Boolean createScope)
      {
         using (Processor.WithCurrentFile(fileName))
         {
            doc = new XmlDocument();
            var processor = createScope ? Processor.CreateChildScope() : Processor;
            doc.LoadXml(processor.Replace(doc.OuterXml));
            return doc;
         }
      }

      public String IncludeXmlFileSafe(String file, String xpath)
      {
         return IncludeXmlFile(file, xpath, scoped: true);
      }

      public String IncludeXmlFile(String file, String xpath)
      {
         return IncludeXmlFile(file, xpath, scoped: false);
      }

      public String IncludeXmlFile(String file, String xpath, Boolean scoped)
      {
         Contract.Requires(xpath != null);
         Contract.Requires(file != null);
         Contract.Ensures(Contract.Result<String>() != null);

         if (!Path.IsPathRooted(file))
            throw Processor.Error("IncludeXmlFile: only absolute paths accepted");

         var contents = IncludeFile(file);
         var doc = new XmlDocument();

         // Maybe the contents are already parseable.
         doc.LoadXml(contents);

         // Remove the xml declaration if it's there.
         var decl = doc.ChildNodes.Cast<XmlNode>().Where(n => n.NodeType == XmlNodeType.XmlDeclaration).FirstOrDefault();
         if (decl != null) doc.RemoveChild(decl);

         doc = ProcessXmlFile(file, doc, scoped);

         if (xpath == null) xpath = "/*"; // document element

         var buffer = new StringBuilder();
         foreach (XmlNode node in doc.SelectNodes(xpath))
            buffer.Append(node.OuterXml);
         return buffer.ToString();
      }

      public String IncludeXmlFileSafe(String file)
      {
         return IncludeXmlFile(file, createScope: true);
      }

      public String IncludeXmlFile(String file)
      {
         return IncludeXmlFile(file, createScope: false);
      }

      /// <summary>
      /// Like <see cref="IncludeFile"/> but also validates the snippet is valid xml by wrapping it in &lt;root&gt;&lt;/root&gt;
      /// if it doesn't parse directly.
      /// </summary>
      public String IncludeXmlFile(String file, Boolean createScope)
      {
         Contract.Requires(file != null);
         Contract.Ensures(Contract.Result<String>() != null);

         if (!Path.IsPathRooted(file))
            throw Processor.Error("IncludeXmlFile: only absolute paths accepted");

         var contents = IncludeFile(file);
         var doc = new XmlDocument();
         var firstPassOK = true;

         // Maybe the contents are already parseable.
         try
         {
            doc.LoadXml(contents);
            // Remove the xml declaration if it's there.
            var decl = doc.ChildNodes.Cast<XmlNode>().Where(n => n.NodeType == XmlNodeType.XmlDeclaration).FirstOrDefault();
            if (decl != null) doc.RemoveChild(decl);
         }
         catch
         {
            // they're not, let's wrap em and try again
            firstPassOK = false;
         }

         if (!firstPassOK)
         {
            doc.LoadXml("<root/>");
            try
            {
               doc.DocumentElement.InnerXml = contents;
            }
            catch (Exception e)
            {
               throw Processor.Error(e, "Failed to load file '{0}' as xml snippet (contained within <root></root>).", file);
            }
         }

         doc = ProcessXmlFile(file, doc, createScope);

         if (doc == null)
            return "";

         if (firstPassOK)
            return doc.OuterXml;
         else
            // Return contents of <root />
            return doc.DocumentElement.InnerXml;
      }

      /// <summary>
      /// Includes all files that match the wildcard. Note that the wildcard can, currently, only be included as part of the filename.
      /// </summary>
      public String IncludeXmlFiles(String wildCardedFile, String xpath)
      {
         Contract.Requires(wildCardedFile != null);
         Contract.Requires(xpath != null);
         Contract.Ensures(Contract.Result<String>() != null);

         // Note: relative files are not currently supported because we don't keep track of currently processing file.
         if (!Path.IsPathRooted(wildCardedFile))
            throw Processor.Error("IncludeXmlFiles: only absolute paths accepted");

         var directory = Path.IsPathRooted(wildCardedFile) ? Path.GetDirectoryName(wildCardedFile) : Directory.GetCurrentDirectory();
         var spec = Path.GetFileName(wildCardedFile);
         var buffer = new StringBuilder();
         foreach (var file in Directory.GetFiles(directory, spec, SearchOption.TopDirectoryOnly))
            buffer.Append(IncludeXmlFile(file, xpath));
         return buffer.ToString();
      }

      public String OptIncludeXmlFile(String file)
      {
         Contract.Requires(file != null);
         Contract.Ensures(Contract.Result<String>() != null);

         if (!Path.IsPathRooted(file))
            throw Processor.Error("OptIncludeXmlFile: only absolute paths accepted");

         if (File.Exists(file)) return IncludeXmlFile(file);
         return "";
      }

      #endregion

      #region Binder Implementation

      private static readonly Expression _sEmptyStringExpr = Expression.Constant("");

      Expression IVariableBinder.BindVariableRead(Expression currentContext, String name)
      {
         Type valueType;
         if (KeyValueStore.TryGetNameInScope(name, out valueType))
         {
            var getValueInScopeMethod = KeyValueStore.GetType().GetMethod("GetValueInScope");
            var getKeyValueStoreExpr = Expression.Field(currentContext, "KeyValueStore");
            return Expression.Convert(Expression.Call(getKeyValueStoreExpr, getValueInScopeMethod, new[] { Expression.Constant(name) }), valueType);
         }

         return AllowUnknownNames ? _sEmptyStringExpr : null;
      }

      public Expression BindVariableWrite(Expression currentContext, String name, Expression value)
      {
         if (this.ReadOnlyVariables.Contains(name))
            throw new InvalidOperationException(String.Format("Variable '{0}' cannot be set because it is marked as read-only.", name));

         KeyValueStore.KnownNames[name] = value.Type;

         var indexer = KeyValueStore.GetType().GetProperty("Item");
         var getKeyValueStoreExpr = Expression.Field(currentContext, "KeyValueStore");
         var indexExpr = Expression.Property(getKeyValueStoreExpr, indexer, new[] { Expression.Constant(name) });
         return Expression.Assign(indexExpr, Expression.Convert(value, typeof(Object)));
      }

      public Expression UnbindVariable(Expression currentContext, String name)
      {
         if (!KeyValueStore.KnownNames.ContainsKey(name)) throw new InvalidOperationException(String.Format("Cannot unset variable '{0}' as it has not been defined.", name));
         KeyValueStore.KnownNames.Remove(name);
         return Expression.Constant(""); // todo better nop
      }

      #endregion
   }
}
