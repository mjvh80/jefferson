﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Jefferson.FileProcessing
{
   public abstract class FileProcessor<TSelf, TContext> : IDynamicMetaObjectProvider
      where TContext : FileScopeContext<TContext, TSelf>
      where TSelf : FileProcessor<TSelf, TContext>
   {
      protected TemplateParser mTemplateParser;
      protected TContext mContext;

      public FileProcessor(TContext context)
      {
         if (context == null) throw new ArgumentNullException(nameof(context));
         if (context.Processor != null) throw new InvalidOperationException("Context should not be re-used accross processors.");

         mContext = context;
         mContext.Processor = (TSelf)this;
         mTemplateParser = BuildTemplateParser();
      }

      public TContext Context { get { return mContext; } }

      public abstract TSelf CreateChildScope();

      protected virtual TemplateParser BuildTemplateParser()
      {
         var jefferson = new TemplateParser();

         jefferson.ValueFilter = (name, value) =>
         {
            Log("Variable '{0}' resolved to: {1}", name, value);
            return value;
         };

         // todo: not sure these events are the right way to extend things.
         // instead maybe better to override TemplateParser here

         jefferson.ReplaceStarted += (_, args) =>
         {
            mContext.ReplaceCount += 1;
            if (mContext.ReplaceCount > mContext.ReplaceLimit) throw new StopProcessingException();
         };

         jefferson.ParserContextCreated += (_, args) =>
         {
            // todo: namespaces are case sensitive in matching here.
            // See default nameresolver in expression parser, type finding needs update.
            foreach (var uniqueNs in mContext.UsingNamespaces.Where(ns => !args.ParserContext.UsingNamespaces.Contains(ns)))
               args.ParserContext.UsingNamespaces.Add(uniqueNs);
         };

         jefferson.PragmaSeen += (_, args) =>
         {
            if (args.Arguments.Equals("dontprocess", StringComparison.OrdinalIgnoreCase))
               throw new StopProcessingException();

            var onceMatch = Regex.Match(args.Arguments.Trim(), "^once(\\s+\\d+)?$", RegexOptions.CultureInvariant);
            if (onceMatch.Success)
               mContext.ReplaceLimit = onceMatch.Groups[1].Success ? Int32.Parse(onceMatch.Groups[1].Value) : 1;
            if (mContext.ReplaceCount > mContext.ReplaceLimit) throw new StopProcessingException();
         };

         return jefferson;
      }

      protected virtual void Log(String msg, params Object[] args)
      {
         // Override to support logging.
      }

      /// <summary>
      /// Override to support custom exception types.
      /// </summary>
      /// <param name="msg"></param>
      /// <param name="args"></param>
      public virtual Exception Error(String msg, params Object[] args)
      {
         return new Exception(String.Format(msg, args));
      }
      public virtual Exception Error(Exception inner, String msg, params Object[] args)
      {
         return new Exception(String.Format(msg, args), inner);
      }
      public virtual Exception Error(XmlNode node, Exception inner, String msg, params Object[] args)
      {
         return new Exception(String.Format(msg, args), inner); // ignores nodes
      }

      #region Processing Support

      /// <summary>
      /// Sets the variable __FILE__ used for debugging.
      /// </summary>
      public void SetCurrentFile(String fileName)
      {
         this.Add("__FILE__", fileName, false, false, false);
      }

      public String GetCurrentFile()
      {
         return GetReplacement("__FILE__");
      }

      public IDisposable WithCurrentFile(String fileName)
      {
         return new _CurrentFileSetter(fileName, (TSelf)this);
      }

      private class _CurrentFileSetter : IDisposable
      {
         public TSelf Processor;
         public String oldFile;
         public _CurrentFileSetter(String fn, TSelf processor)
         {
            Processor = processor;
            oldFile = Processor.GetCurrentFile();
            Processor.SetCurrentFile(fn);
         }
         public void Dispose()
         {
            Processor.SetCurrentFile(oldFile);
         }
      }

      private static String _ReadFileContents(String fn)
      {
         try
         {
            return File.ReadAllText(fn);
         }
         catch (Exception e)
         {
            throw new Exception(String.Format("Failed to read file '{0}'.", fn), e);
         }
      }

      public String GetReplacement(String key)
      {
         if (String.IsNullOrEmpty(key)) return key;
         key = key.Trim('$');
         Object result;
         if (mContext.KeyValueStore.TryGetValueInScope(key, out result))
            return result?.ToString();
         return null;
      }

      public String GetReplacedFile(String fn)
      {
         return Replace(_ReadFileContents(fn));
      }

      /// <summary>
      /// Processes the given hierarchy by processing all files in a childscope for its parent.
      /// </summary>
      /// <param name="hierarchy"></param>
      /// <param name="deepReplacement">Replace until there are no further $$ markers.</param>
      public void ProcessFileHierarchy(IFileHierarchy hierarchy, Boolean deepReplacement = true)
      {
         if (hierarchy == null)
         {
             throw new ArgumentNullException(nameof(hierarchy), "Contract assertion not met: hierarchy != null");
         }
         _ProcessFileHierarchy((TSelf)this, hierarchy, deepReplacement);
      }

      private void _ProcessFileHierarchy(TSelf fileScope, IFileHierarchy hierarchy, Boolean deepReplacement)
      {
         foreach (var file in hierarchy.Files)
            _WriteFile(file.TargetFullPath, fileScope.Replace(_ReadFile(file.SourceFullPath, file.Encoding), deepReplacement), file.Encoding);

         foreach (var directory in hierarchy.Children)
            _ProcessFileHierarchy(fileScope.CreateChildScope(), directory, deepReplacement);
      }

      private static void _WriteFile(String file, String contents, Encoding encoding)
      {
         try
         {
            File.WriteAllText(file, contents, encoding);
         }
         catch (Exception e)
         {
            throw new IOException("Could not write file " + file, e);
         }
      }

      private static String _ReadFile(String file, Encoding encoding)
      {
         try
         {
            return File.ReadAllText(file, encoding);
         }
         catch (Exception e)
         {
            throw new IOException("Could not read file " + file, e);
         }
      }

      /// <summary>
      /// Replaces variable expressions in the given source.
      /// Note about <see cref="except"/>, it only causes no exceptions to be thrown if a *name* cannot be resolved. Invalid
      /// expressions will still cause errors.
      /// </summary>
      /// <param name="source"></param>
      /// <param name="except">If true throw an exception if a variable (name) has not been defined.</param>
      /// <returns></returns>
      public String Replace(String source, Boolean deep = true)
      {
         if (String.IsNullOrEmpty(source)) return "";

         mContext.ReplaceCount = 0;
         mContext.ReplaceLimit = Int32.MaxValue;

         try
         {
            if (deep)
               return mTemplateParser.ReplaceDeep(source, mContext);
            else
               return mTemplateParser.Replace(source, mContext);
         }
         catch (Exception e)
         {
            throw Error(e, "Unexpected exception replacing variables in source.");
         }
      }

      /// <summary>
      /// If isExpr is true, valueExpr is treated as an expression. Otherwise it is treated as a string
      /// in which variables are replaced. Note that e.g.
      ///  - Add("key", "true", true): sets the value for "key" to the *bool* true
      ///  - Add("key", "true", false): sets the value for key to the *string* "true".
      /// </summary>
      /// <param name="key">The key to set, may be wrapped in $$ but these are removed.</param>
      /// <param name="valueExpr">The value (string) or expression, this depends on the isExpr value.</param>
      /// <param name="isExpr">
      /// Marks that the value should be executed as an expression against the given context. Otherwise it is treated as a string that may contain
      /// variables. Note that this evaluation/replacement is dynamic, and is performed every time by default (unless const is true). This means that the expression may provide
      /// different values or replacement results depending on when it is run.
      /// </param>
      public void Add(String key, String valueExpr, Boolean isExpr = false, Boolean except = false, Boolean @readonly = false)
      {
         if (key == null) throw new ArgumentNullException(nameof(key));
         if (key.Contains(".")) throw Error("VarReplacer key should not contain a period (.): " + key);

         key = key.Trim('$');

         var haveKey = Context.ReadOnlyVariables.Contains(key);
         if (@readonly && !haveKey)
            Context.ReadOnlyVariables.Add(key);
         else if (haveKey)
            throw Error("Key already exists in some scope in VarReplacer and is marked as readonly: " + key);

         if (isExpr)
            mContext.KeyValueStore[key] = mTemplateParser.EvaluateExpression(valueExpr, mContext); // _EvaluateExpression(valueExpr, except, new List<Object> { _mContext });
         else
            mContext.KeyValueStore[key] = Replace(valueExpr);
      }

      public void AddFromNodes(XmlNodeList vars, Boolean @fixed = false)
      {
         if (vars == null)
         {
             throw new ArgumentNullException(nameof(vars), "Contract assertion not met: vars != null");
         }

         if (vars.Count == 0) return;

         foreach (XmlElement variables in vars)
         {
            if (variables.GetAttributeNode("flags") != null)
               this.Context.AllowUnknownNames = XmlUtils.OptReadBool(variables, "@allow-unknown-names", false, this.Error);

            mContext.UsingNamespaces.AddRange(variables.GetAttribute("using").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries));

            foreach (XmlElement variable in variables.SelectNodes("*").Cast<XmlElement>().Where(var => var.HasAttribute("from") || var.HasAttribute("name")))
            {
               var markedReadOnly = XmlUtils.OptReadBool(variable, "@readonly", false, this.Error);

               if (variable.HasAttribute("from") && variable.HasAttribute("name"))
                  throw this.Error(variable, null, "variable definition should use either @from or @name, not both");

               if (variable.HasAttribute("to") && variable.HasAttribute("value"))
                  throw this.Error(variable, null, "variable definition should use either @to or @value, not both");

               // If there's a @to (or @value) attribute use that, otherwise we'll use the innertext. 
               var valueAttribute = variable.GetAttributeNode("to") ?? variable.GetAttributeNode("value");

               // It's important to get the unescaped xml here, so don't use .Value on attributes.
               var txt = valueAttribute == null ? variable.InnerXml : valueAttribute.InnerXml;

               // By default the given text is a *replacement* operation, thus, resulting type is String (text).
               // Setting isExpr to true, however, ensures that the txt is parsed entirely as an expression with its resulting type.
               var isExpr = XmlUtils.OptReadBool(variable, "@isexpr", false, this.Error);
               var exceptOnError = XmlUtils.OptReadBool(variable, "@strict", false, this.Error);

               if (variable.GetAttributeNode("enum") != null)
               {
                  var @enum = XmlUtils.ReadStr(variable, "@enum", this.Error);
                  var enumType = _OptFindType(@enum);
                  if (enumType == null) throw Error("Enum type {0} could not be found.", @enum);
                  // Note: we could double check this is actually an enum, but I won't bother for now in the interest of performance.

                  try { Enum.Parse(enumType, txt, true); } // Note: tryparse is only generic
                  catch (Exception e) { throw Error(e, "Invalid enumeration value {0}.", txt); }

                  isExpr = true;
                  txt = enumType.FullName + "." + txt;
               }

               var key = XmlUtils.ReadStr(variable, "@from | @name", this.Error);
               Add(key, txt, isExpr, exceptOnError, @readonly: markedReadOnly);
            }
         }
      }

      public void Clear()
      {
         mContext.KeyValueStore.Clear();
      }

      // todo: deal with conflicts
      private static Type _OptFindType(String fullName)
      {
         var result = Type.GetType(fullName, throwOnError: false, ignoreCase: true);
         if (result != null) return result;

         foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            try
            {
               result = asm.GetType(fullName, throwOnError: false, ignoreCase: true);
               if (result != null) return result;
            }
            catch { }

         return null;
      }

      #endregion

      #region Dynamic Support

      public Object GetObject(String key)
      {
         Object result;
         if (mContext.KeyValueStore.TryGetValueInScope(key, out result))
            return result;
         return null;
      }

      private class MetaObject : DynamicMetaObject
      {
         public readonly FileProcessor<TSelf, TContext> Replacer;

         public MetaObject(Expression expr, FileProcessor<TSelf, TContext> replacer) : base(expr, BindingRestrictions.Empty, replacer) { Replacer = replacer; }

         public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
         {
            if (binder.CallInfo.ArgumentCount != 1)
               return binder.FallbackGetIndex(this, indexes);


            return _BindNameLookup(indexes[0].Expression);
         }

         public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
         {
            return _BindNameLookup(Expression.Constant(binder.Name));
         }

         public override IEnumerable<String> GetDynamicMemberNames()
         {
            return Replacer.mContext.KeyValueStore.AllKeysInScope;
         }

         private DynamicMetaObject _BindNameLookup(Expression nameExpr)
         {
            var expr = Expression.Call(Expression.Convert(this.Expression, this.LimitType),
                                    Replacer.GetType().GetMethod("GetObject", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new[] { typeof(String) }, null),
                                    nameExpr);

            return new DynamicMetaObject(expr, BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType));
         }
      }

      DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
      {
         return new MetaObject(parameter, this);
      }

      #endregion
   }
}
