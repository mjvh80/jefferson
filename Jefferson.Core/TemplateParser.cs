/*   
   Copyright 2014 Marcus van Houdt

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */
using Jefferson.Directives;
using Jefferson.Extensions;
using Jefferson.Output;
using Jefferson.Parsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;


namespace Jefferson
{
   /// <summary>
   /// Parses a template and returns a lambda expression accepting an object and an outputwriter as input.
   /// </summary>
   public class TemplateParser
   {
      /// <summary>
      /// Returns the list of default directives supported. These are:
      /// if, each, let, block, define, undef, comment, pragma and using
      /// </summary>
      /// <returns></returns>
      public static IDirective[] GetDefaultDirectives()
      {
         return new IDirective[] { new IfDirective(), new EachDirective(), new LetDirective(), new BlockDirective(), new DefineDirective(), new UndefDirective(), new CommentDirective(), new PragmaDirective(), new UsingDirective() };
      }

      /// <summary>
      /// Creates a parser with default directives registered.
      /// </summary>
      public TemplateParser() : this(GetDefaultDirectives()) { }

      /// <summary>
      /// Creates a template parser with the given directives and uses default options.
      /// </summary>
      public TemplateParser(params IDirective[] directives) : this(new TemplateOptions(), directives) { }

      public TemplateParser(TemplateOptions options) : this(options, GetDefaultDirectives()) { }

      /// <summary>
      /// Creates a parser with the given directives.
      /// </summary>
      public TemplateParser(TemplateOptions options, params IDirective[] directives)
      {
         var reservedWords = new HashSet<String>();
         _mDirectiveMap = new Dictionary<String, IDirective>(directives == null ? 0 : directives.Length, StringComparer.Ordinal);
         if (directives != null)
            foreach (var directive in directives.Where(d => d != null))
            {
               if (!_sDirectiveNameExpr.IsMatch(directive.Name))
                  throw Utils.Error("Directive '{0}' contains invalid characters.");

               if (_mDirectiveMap.ContainsKey(directive.Name))
                  throw Utils.Error("Directive '{0}' already defined.", directive.Name);

               if (reservedWords.Contains(directive.Name))
                  throw Utils.Error("Directive '{0}' clashes with a reserved word from another directive.", directive.Name);

               if (directive.ReservedWords != null && directive.ReservedWords.Any(k => _mDirectiveMap.ContainsKey(k)))
                  throw Utils.Error("A reserved word for directive '{0}' matches a directive that has already been registered.", directive.Name);

               if (directive.ReservedWords != null)
                  foreach (var word in directive.ReservedWords) reservedWords.Add(word);

               _mDirectiveMap.Add(directive.Name, directive);
            }

         Options = options ?? new TemplateOptions();
      }

      private readonly Dictionary<String, IDirective> _mDirectiveMap;
      private static readonly Regex _sDirectiveNameExpr = new Regex("^[a-zA-Z]+$", RegexOptions.CultureInvariant); // for now

      public TemplateOptions Options { get; private set; }

      /// <summary>
      /// Convenience method. Calls Parse and compiles the resulting expression tree.
      /// </summary>
      public Action<TContext, IOutputWriter> Compile<TContext>(String source, Type contextType = null, IVariableBinder binder = null)
      {
         Contract.Requires(source != null);
         Contract.Ensures(Contract.Result<Action<TContext, IOutputWriter>>() != null);
         return Parse<TContext>(source, contextType, binder).Compile();
      }

      /// <summary>
      /// Parse the given source into an expression tree.
      /// </summary>
      /// <typeparam name="TContext">The type of the input value. Note that if contextType != typeof(TContext) then contextType must be a subclass of the context.</typeparam>
      /// <param name="source">Source code in which occurrences of template markers are replaced.</param>
      /// <param name="contextType">The actual type of the input value.</param>
      /// <param name="binder">Variable binder, see <see cref="IVariableBinder"/></param>
      public Expression<Action<TContext, IOutputWriter>> Parse<TContext>(String source, Type contextType = null, IVariableBinder binder = null)
      {
         Contract.Requires(source != null);
         Contract.Ensures(Contract.Result<Expression>() != null);

         if (contextType == null) contextType = typeof(TContext);

         var ctx = new TemplateParserContext(this, source)
         {
            ContextTypes = new List<Type> { contextType },
            ContextDeclarations = new List<IVariableBinder> { binder },
            DirectiveMap = _mDirectiveMap,
            UserProvidedValueFilter = ValueFilter,
            UserProvidedOutputFilter = OutputFilter,
            Options = this.Options
         };

         return ctx.Parse<TContext>(source);
      }

      /// <summary>
      /// Compiles and runs the given source and input context. In effect this interprets the source codes rather than compiling it.
      /// </summary>
      public String Replace(String source, Object context)
      {
         Contract.Requires(source != null);
         Contract.Requires(context != null);
         Contract.Ensures(Contract.Result<String>() != null);

         var tree = Parse<Object>(source, context.GetType(), context as IVariableBinder);
         var buffer = new StringBuilder();
         tree.Compile()(context, new StringBuilderOutputWriter(buffer));
         return buffer.ToString();
      }

      /// <summary>
      /// Keeps replacing expressions until none found.
      /// </summary>
      public String ReplaceDeep(String source, Object context)
      {
         Contract.Requires(source != null);
         Contract.Requires(context != null);
         Contract.Ensures(Contract.Result<String>() != null);

         var loop = 1;
         do
         {
            // If you need more than 1000 iterations, just do as this code without the loop "detection".
            if (loop > 1000) // say
               throw Utils.Error("Possible loop detected in ReplaceDeep, stopping after 1000 iterations.");

            source = Replace(source, context);
            loop += 1;
         }
         while (source.IndexOf("$$") >= 0);

         return source;
      }

      /// <summary>
      /// Interprets an expression against a given context and returns the result.
      /// As this compiles and runs on the fly, this is not expected to be fast.
      /// </summary>
      public Object EvaluateExpression(String expr, Object context)
      {
         Contract.Requires(expr != null);
         Contract.Requires(context != null);

         var ctx = new TemplateParserContext(this, "")
         {
            ContextTypes = new List<Type> { context.GetType() },
            ContextDeclarations = new List<IVariableBinder> { context as IVariableBinder },
            UserProvidedValueFilter = ValueFilter,
            UserProvidedOutputFilter = OutputFilter,
            Options = this.Options
         };

         return ctx.EvaluateExpression<Object, Object>(expr, context);
      }

      public event Action<Object, PragmaEventArgs> PragmaSeen;

      internal void OnPragmaSeen(TemplateParserContext context, String arguments)
      {
         if (PragmaSeen != null)
            PragmaSeen(this, new PragmaEventArgs(context, arguments));
      }

      public Func<String, Object, Object> ValueFilter { get; set; }

      // todo: is this useful? Perhaps for standard output encoding?
      // > trimming?
      private Func<String, String> OutputFilter { get; set; }
   }

   public sealed class PragmaEventArgs : EventArgs
   {
      public readonly TemplateParserContext ParserContext;
      public readonly String Arguments;

      // todo: location?

      public PragmaEventArgs(TemplateParserContext context, String args)
      {
         Contract.Requires(context != null);
         Contract.Requires(!String.IsNullOrEmpty(args));

         ParserContext = context;
         Arguments = args;
      }
   }

   namespace Parsing
   {
      /// <summary>
      /// Represents state of the parser during parsing.
      /// </summary>
      public class TemplateParserContext
      {
         internal TemplateParserContext(TemplateParser parser, String source)
         {
            Contract.Requires(source != null);

            Source = source;
            Output = Expression.Parameter(typeof(IOutputWriter), "output");
            PositionOffsets = new Stack<Int32>();
            Parser = parser;
         }

         internal Dictionary<String, IDirective> DirectiveMap;

         internal List<Type> ContextTypes;
         internal List<IVariableBinder> ContextDeclarations; // < todo rename to ContextBinders?
         internal readonly Stack<Int32> PositionOffsets;

         internal Func<String, Object, Object> UserProvidedValueFilter;
         internal Func<String, String> UserProvidedOutputFilter;

         internal List<String> UsingNamespaces = new List<String>(0);

         public Boolean? OverrideAllowUnknownNames { get; set; }

         public TemplateOptions Options { get; internal set; }

         public readonly TemplateParser Parser;

         /// <summary>
         /// Represents the runtime List&lt;Object&gt;, the stack of current contexts (or scopes).
         /// </summary>
         public ParameterExpression RuntimeContexts { get; private set; }

         /// <summary>
         /// The parameter representing an instance of <see cref="IOutputWriter"/> used for output.
         /// </summary>
         public ParameterExpression Output { get; private set; }

         /// <summary>
         /// The global source being parsed.
         /// </summary>
         public readonly String Source;

         public IVariableBinder ReplaceCurrentVariableBinder(IVariableBinder binder)
         {
            // note: binder may be null
            var current = ContextDeclarations[ContextDeclarations.Count - 1];
            ContextDeclarations[ContextDeclarations.Count - 1] = binder;
            return current;
         }

         /// <summary>
         /// Pushes a scope which is a Type for the current scope/context and variable declarations for this scope.
         /// </summary>
         public void PushScope(Type context, IVariableBinder binder = null)
         {
            Contract.Requires(context != null);
            ContextTypes.Add(context);
            ContextDeclarations.Add(binder);
         }

         /// <summary>
         /// Pops the current scope, see <see cref="PushScope"/>.
         /// </summary>
         public void PopScope()
         {
            var count = ContextTypes.Count;
            Contract.Assert(count == ContextDeclarations.Count);
            ContextTypes.RemoveAt(count - 1);
            ContextDeclarations.RemoveAt(count - 1);
         }

         /// <summary>
         /// The type for the current top most context/scope.
         /// </summary>
         public Type CurrentContextType
         {
            get { return ContextTypes[ContextTypes.Count - 1]; }
         }

         /// <summary>
         /// Variable declarations belonging to the current context.
         /// </summary>
         public IVariableBinder CurrentVariableDeclaration
         {
            get { return ContextDeclarations[ContextTypes.Count - 1]; }
         }

         /// <summary>
         /// Current position offset into global source.
         /// </summary>
         public Int32 CurrentPositionOffset
         {
            get
            {
               return PositionOffsets.Count == 0 ? 0 : PositionOffsets.Peek();
            }
         }

         /// <summary>
         /// Calculate the position in the global source.
         /// </summary>
         public Int32 GetPosition(Int32 relativePosition)
         {
            return CurrentPositionOffset + relativePosition;
         }

         /// <summary>
         /// Get an expression that, at runtime, gets the nth context. The 0th context is the current context, the 1st context
         /// is its parent and so on. This corresponds to $x expressions where $0 is the 0th context.
         /// </summary
         public Expression GetNthContext(Int32 n)
         {
            Contract.Requires(n >= 0);
            Contract.Ensures(Contract.Result<Expression>() != null);
            Contract.Assert(RuntimeContexts.Type == typeof(List<Object>));

            var indexer = typeof(List<Object>).GetProperty("Item");
            Contract.Assume(indexer != null);

            var idx = ContextTypes.Count - 1 - n;
            return Expression.Convert(Expression.MakeIndex(RuntimeContexts, indexer, new[] { Expression.Constant(idx) }),
                                      ContextTypes[idx]);
         }

         #region Error Support

         /// <summary>
         /// Throws a <see cref="SyntaxException"/> by also translating the position to the global source.
         /// </summary>
         public Exception SyntaxError(Int32 relativeIndex, String msg, params Object[] args)
         {
            return SyntaxException.Create(this.Source, GetPosition(relativeIndex), msg, args);
         }

         public Exception SyntaxError(Int32 relativeIndex, Exception inner, String msg, params Object[] args)
         {
            Contract.Requires(inner != null);
            return SyntaxException.Create(inner, Source, GetPosition(relativeIndex), msg, args);
         }

         #endregion

         /// <summary>
         /// Beef.
         /// </summary>
         public Expression<Action<TContext, IOutputWriter>> Parse<TContext>(String source)
         {
            Contract.Requires(source != null);

            if (!(typeof(TContext).IsAssignableFrom(CurrentContextType)))
               throw Utils.InvalidOperation("Invalid Compile call, generic argument type '{0}' is not a baseclass for current context type '{1}'.", typeof(TContext).FullName, CurrentContextType.FullName);

            // See if we can find jefferson.
            var idx = source.IndexOf("$$", 0, StringComparison.Ordinal);
            if (idx < 0)
            {
               if (Options.EnableTracing) Trace.WriteLine("No Jefferson markers found, outputting entire source.");
               return (c, sb) => sb.Write(source);
            }

            // The main context object, input to the resulting delegate.
            var contextParam = Expression.Parameter(typeof(TContext), "context");
            var contextParamAsObj = Expression.Convert(contextParam, typeof(Object));

            var outputParam = this.Output;

            var writeMethod = Utils.GetMethod<IOutputWriter>(b => b.Write(""));

            var bodyStmts = new List<Expression>();

            var runtimeCtxs = Expression.Variable(typeof(List<Object>), "contexts");
            if (RuntimeContexts == null)
            {
               bodyStmts.Add(Expression.Assign(runtimeCtxs, Expression.New(typeof(List<Object>))));
               bodyStmts.Add(Expression.Call(runtimeCtxs, Utils.GetMethod<List<Object>>(l => l.Add(null)), contextParam));
               this.RuntimeContexts = runtimeCtxs;
            }
            else
               bodyStmts.Add(Expression.Assign(runtimeCtxs, RuntimeContexts));

            if (Options.EnableTracing) bodyStmts.Add(Utils.GetSimpleTraceExpr("Starting processing."));

            var prevIdx = 0;
            while (idx >= 0 && idx < source.Length)
            {
               var chunk = source.Substring(prevIdx, idx - prevIdx);

               if (Options.EnableTracing) bodyStmts.Add(Utils.GetSimpleTraceExpr("Emitting source chunk of length {0}", chunk.Length));
               bodyStmts.Add(Expression.Call(outputParam, writeMethod, Expression.Constant(chunk)));

               String expression = null;
               if (idx < source.Length - 6 && source[idx + 2] == '#')
               {
                  // We are parsing a directive.
                  // Find the name of the directive, and pass on control.
                  var dirNameEndIdx = Utils.MinNonNeg(source.IndexOfWhiteSpace(idx + 3), source.IndexOf("/$$", idx + 3), source.IndexOf("$$", idx + 3));
                  if (dirNameEndIdx < 0) throw SyntaxError(idx, "Could not find end of directive.");

                  var dirBodyStartIdx = source.IndexOf("$$", idx + 3);
                  if (dirBodyStartIdx < 0) throw SyntaxError(idx, "Could not find end of directive.");
                  dirBodyStartIdx += 2; // skip $$

                  var directiveName = source.Substring(idx + 3, dirNameEndIdx - idx - 3);
                  IDirective directive;
                  if (!DirectiveMap.TryGetValue(directiveName, out directive))
                     throw SyntaxError(idx, "Could not find directive '{0}'.", directiveName);

                  /* Note: empty directives are required to end with /$$ to keep the parser simple.
                   * Otherwise we'd need to ask directives if they are empty based on their arguments.
                   * I also think it is more clear from source that there's no end to be expected. */
                  var isEmpty = source[dirBodyStartIdx - 3] == '/';

                  var directiveEnd = isEmpty ? "/$$" : "$$/" + directiveName;
                  var directiveEndIdx = isEmpty ? dirBodyStartIdx - directiveEnd.Length : FindDirectiveEnd(source, dirNameEndIdx, directive is LiteralDirective, directiveEnd);
                  if (directiveEndIdx < 0) throw SyntaxError(idx, "Failed to find directive end '{0}' for directive '{1}'.", directiveEnd, directiveName);
                  if (!isEmpty && dirBodyStartIdx > directiveEndIdx) throw SyntaxError(idx, "Could not find end of directive.");

                  // Mark where to continue parsing.
                  prevIdx = directiveEndIdx + directiveEnd.Length;
                  if (!isEmpty)
                  {
                     prevIdx = source.IndexOf("$$", directiveEndIdx + directiveEnd.Length);
                     if (prevIdx < 0) throw SyntaxError(directiveEndIdx, "Missing '$$' directive end.");
                     var endPart = source.Substring(directiveEndIdx + directiveEnd.Length, prevIdx - directiveEndIdx - directiveEnd.Length);
                     if (endPart.Length > 0 && !String.IsNullOrWhiteSpace(endPart)) // todo avoid substring
                        throw SyntaxError(directiveEndIdx + directiveEnd.Length, "Invalid characters found, expected only possible whitespace.");
                     prevIdx += 2;
                  }

                  if (Options.EnableTracing) bodyStmts.Add(Utils.GetSimpleTraceExpr("Compiling directive #" + directiveName));

                  // Add the compiled directive to the body.
                  // Keep track of line numbers in global source.
                  PositionOffsets.Push(CurrentPositionOffset + dirBodyStartIdx);

                  var directiveSource = isEmpty ? null : source.Substring(dirBodyStartIdx, directiveEndIdx - dirBodyStartIdx);

                  bodyStmts.Add(directive.Compile(this, arguments: source.Substring(dirNameEndIdx, dirBodyStartIdx - dirNameEndIdx - (isEmpty ? 3 : 2)).Trim(),
                                                        source: directiveSource));

                  PositionOffsets.Pop();
               }
               else if (idx + 2 < source.Length && source[idx + 2] == '/' && (idx + 3 == source.Length || source[idx + 3] != '/'))
                  throw SyntaxError(idx, "Unexpected '{0}' found.", source.Substring(idx, 3)); // todo: improve this error
               else
               {
                  // Simpler replacement expression.
                  var closeIdx = source.IndexOf("$$", idx + 2);
                  if (closeIdx < 0)
                     throw SyntaxError(idx, "Found $$ but could not find matching end.");

                  expression = source.Substring(idx + 2, closeIdx - idx - 2);
                  var allowUnknownNames = expression.At(0) == '?';
                  if (allowUnknownNames) expression = expression.Substring(1);

                  prevIdx = closeIdx + 2;

                  if (Options.EnableTracing) bodyStmts.Add(Utils.GetSimpleTraceExpr("Emitting value of expression $${0}$$", expression));

                  var oldOverride = this.OverrideAllowUnknownNames;
                  if (allowUnknownNames) this.OverrideAllowUnknownNames = allowUnknownNames;

                  var compExpr = CompileExpression<Object>(expression).Ast;

                  this.OverrideAllowUnknownNames = oldOverride;

                  // Convert the object result to string. Todo: add a Write(Object) method instead?
                  bodyStmts.Add(Expression.Call(outputParam, Utils.GetMethod<IOutputWriter>(sb => sb.Write(null)),
                                   Expression.Call(
                                      null,
                                      Utils.GetMethod(() => Utils.ToString(null)),
                                      Expression.Invoke(compExpr, contextParamAsObj))));
               }

               if (prevIdx >= source.Length) break;
               idx = source.IndexOf("$$", prevIdx, StringComparison.Ordinal);
            }

            if (prevIdx < source.Length)
            {
               var lastChunk = source.Substring(prevIdx);
               if (Options.EnableTracing) bodyStmts.Add(Utils.GetSimpleTraceExpr("Emitting last chunk of length {0}", lastChunk.Length));
               bodyStmts.Add(Expression.Call(outputParam, writeMethod, Expression.Constant(lastChunk)));
            }

            if (Options.EnableTracing) bodyStmts.Add(Utils.GetSimpleTraceExpr("Processing completed."));

            // Construct the final expression.
            return Expression.Lambda<Action<TContext, IOutputWriter>>(Expression.Block(new[] { runtimeCtxs }, bodyStmts), contextParam, outputParam);
         }

         /// <summary>
         /// Finds the given "block" terminators, e.g. for #if one wants to look for $$#else$$, $$#elif$$ or $$/if$$.
         /// This is a convenience method that can handle nested directives.
         /// 
         /// Note: this method causes some unnecessary re-parsing of source in case of nested directives, but as long as this
         /// is not a problem I'll keep this in favour of more complicated models (e.g. an ast or whatever).
         /// </summary>
         public Int32 FindDirectiveEnd(String source, Int32 start, params String[] terminators)
         {
            return FindDirectiveEnd(source, start, false, terminators);
         }

         internal Int32 FindDirectiveEnd(String source, Int32 start, Boolean ignoreNesting, params String[] terminators)
         {
            Ensure.NotNull(source, "source");

            if (terminators == null || terminators.Length == 0)
               return -1;

            // Find the next directive.
            // If we see something like #else it is not a directive because it doesn't have a matching /else.
            var nestedStartIdx = -1;
            var nestedAfterEndIdx = -1;
            if (!ignoreNesting)
               for (var nextStart = start; nestedAfterEndIdx < 0; )
               {
                  nestedStartIdx = source.IndexOf("$$#", nextStart);
                  if (nestedStartIdx < 0) break;

                  nextStart = source.IndexOf("$$", nestedStartIdx + 3);
                  if (nextStart < 0) throw SyntaxError(nestedStartIdx, "Could not find matching $$.");
                  nextStart += 2;

                  var dirIdx = source.IndexOfWhiteSpace(nestedStartIdx + "$$#".Length); // look for $$#<word><space>...$$
                  if (dirIdx < 0 || dirIdx > nextStart - 2) // or $$#<word>$$
                     dirIdx = nextStart - 2;

                  var len = dirIdx - nestedStartIdx - "$$#".Length;
                  if (len == 0) throw SyntaxError(nestedStartIdx, "Invalid empty directive found.");

                  var nestedDirective = source.Substring(nestedStartIdx + "$$#".Length, len);
                  var nestedEmptyIdx = source.IndexOf("/$$", nestedStartIdx + "$$#".Length);
                  if (source.IndexOf("$$", nestedStartIdx + "$$#".Length) < nestedEmptyIdx) nestedEmptyIdx = -1;
                  nestedDirective = "$$/" + nestedDirective;

                  nestedAfterEndIdx = nestedEmptyIdx >= 0 ? nestedEmptyIdx : FindDirectiveEnd(source, nextStart + 2, false, nestedDirective);
                  if (nestedAfterEndIdx >= 0)
                     nestedAfterEndIdx += nestedEmptyIdx >= 0 ? "/$$".Length : nestedDirective.Length;
               }

            // Find the first terminator. It should be suffixed with either $ or whitespace.
            var endIdx = Utils.MinNonNeg(terminators.Select(t => t.TrimEnd('$')).Select(t => source.IndexOfExpr(Regex.Escape(t) + @"(\s+|\$)", start)));

            if (nestedStartIdx < 0) return endIdx; // no nested directive
            if (endIdx < 0) return endIdx; // error: not found

            if (endIdx <= nestedStartIdx) return endIdx; // not nested

            var endOfStart = source.IndexOf("$$", nestedStartIdx + "$$#".Length);
            if (endOfStart < 0) throw SyntaxError(nestedStartIdx, "Could not find ending $$ for directive starting.");

            // Restart search after nested directive.
            if (nestedAfterEndIdx < 0) return -1; // got nowehere else to go

            Contract.Assert(!ignoreNesting);
            return FindDirectiveEnd(source, nestedAfterEndIdx, false, terminators);
         }

         public CompiledExpression<Object, TOutput> CompileExpression<TOutput>(String expr)
         {
            Contract.Requires(expr != null);
            Contract.Ensures(Contract.Result<CompiledExpression<Object, TOutput>>() != null);

            var ignore = 0;
            return _CompileExpression<TOutput>(expr, 0, 0, out ignore);
         }

         public CompiledExpression<Object, TOutput> CompileExpression<TOutput>(String expr, Int32 startAt, out Int32 stoppedAt)
         {
            Contract.Requires(expr != null);
            Contract.Ensures(Contract.Result<CompiledExpression<Object, TOutput>>() != null);
            return _CompileExpression<TOutput>(expr, ExpressionParsingFlags.AllowEarlyStop, startAt, out stoppedAt);
         }

         private CompiledExpression<Object, TOutput> _CompileExpression<TOutput>(String expr, ExpressionParsingFlags flags, Int32 startAt, out Int32 stoppedAt)
         {
            Contract.Requires(expr != null);
            Contract.Ensures(Contract.Result<CompiledExpression<Object, TOutput>>() != null);

            var parser = new ExpressionParser<Object, TOutput>();

            // Parse the expression, compile it and run it.

            flags |= ExpressionParsingFlags.EmptyExpressionIsEmptyString; // this allows $$$$ in source (useful for things like $$//...$$)

            if (this.Options.IgnoreCase)
               flags |= ExpressionParsingFlags.IgnoreCase;

            if (this.Options.UseCurrentCulture)
               flags |= ExpressionParsingFlags.UseCurrentCulture;

            return parser._ParseExpressionInternal(expr, startAt, out stoppedAt, ResolveName, flags, this.CurrentContextType, this.UserProvidedValueFilter, this.UsingNamespaces.ToArray());
         }

         /// <summary>
         /// Interprets the given expression against the given context. This compiles and executes on the fly so this is
         /// not expected to be fast.
         /// </summary>
         public TOutput EvaluateExpression<TContext, TOutput>(String expr, TContext context)
         {
            Contract.Requires(expr != null);

            Contract.Assert(RuntimeContexts == null);
            if (RuntimeContexts != null)
               throw Utils.InvalidOperation("EvaluateExpression cannot be used if Parse is used");

            var contextParam = Expression.Parameter(typeof(TContext), "context");
            var contextParamAsObj = Expression.Convert(contextParam, typeof(Object));
            this.RuntimeContexts = Expression.Variable(typeof(List<Object>), "contexts");
            return
               Expression.Lambda<Func<TContext, TOutput>>(
                  Expression.Block(new[] { RuntimeContexts },
                     Expression.Assign(RuntimeContexts, Expression.New(typeof(List<Object>))),
                     Expression.Call(RuntimeContexts, Utils.GetMethod<List<Object>>(l => l.Add(null)), contextParam),
                     Expression.Invoke(CompileExpression<TOutput>(expr).Ast, contextParamAsObj)),
                  contextParam)
                  .Compile()(context);
         }

         private static readonly Regex _sParentExpr = new Regex(@"^\$\d+$");

         /*
            Creates a name resolver that first checks the current scope (key value store) and only then checks the context
            class for properties and fields etc.

            $$#each Foobars$$

               $$ blah + $0.foobar $$

            $$/each$$

            Idea: $0 is a namespace rather than a name.

            This means that $$ GetScope().foobar $$ won't work dynamically.

            So:

            .properties: never dynamic, always actual properties.

            This is because we cannot know what we're resolving the name on.

            In general name resolution then works by resolving against the current context and otherwise moving up:

              - default resolution (parser), if succeeded -> used
               - is a variable declared, i.e. is type known?
               - if not, move up and repeat

            If the name $0.x is seen, normal resolution will attempt to find $0, which should fail in general (invalid C# identifier).
            We then attempt resolution with typename = $0 and name = x. This is then resolved against the correct context depending on n in $n, chaining as expected.
         */
         private Expression ResolveName(Expression thisExpr, String name, String typeName, NameResolverDelegate defaultResolver)
         {
            Contract.Assert(thisExpr.Type == GetNthContext(0).Type);

            var startIndex = 0;

            if (typeName != null)
            {
               if (_sParentExpr.IsMatch(typeName))
               {
                  Contract.Assume(typeName.Length > 1);
                  startIndex = Int32.Parse(typeName.Substring(1));

                  // special name which moves up the context stack.
                  if (startIndex > ContextTypes.Count - 1)
                     // TODO: positional info is wrong here we need to get it from the expression parser
                     throw SyntaxError(0, "Invalid parent context '{0}': number of contexts in chain is {1}", typeName, ContextTypes.Count.ToStringInvariant());
               }
               else
                  // Anything else of the form a.b.c we don't resolve here.
                  return defaultResolver(thisExpr, name, typeName, null);
            }

            // Resolve up till root context.
            for (var currentContext = GetNthContext(startIndex); ; )
            {
               // See if a variable has been declared.
               var decls = ContextDeclarations[ContextDeclarations.Count - 1 - startIndex];

               var variableBinding = decls == null ? null : decls.BindVariableRead(currentContext, name); //varType decls.GetType(name);

               // todo: validate resulting type?
               if (variableBinding != null)
                  return variableBinding;

               var baseResolve = defaultResolver(currentContext, name, typeName, null);
               if (baseResolve != null) return baseResolve;

               startIndex += 1;
               if (startIndex == ContextTypes.Count) break;
            }

            var allowUnknownNames = OverrideAllowUnknownNames ?? Options.AllowUnknownNames;

            var result = defaultResolver(thisExpr, name, typeName, null);
            if (result == null && allowUnknownNames)
               return Expression.Default(typeof(Object));
            return result;
         }

         /// <summary>
         /// Used to assign a variable at runtime (e.g. using #define).
         /// </summary>
         public Expression SetVariable(Expression thisExpr, String name, Int32 relativePositionInSource, Expression @value)
         {
            Contract.Requires(thisExpr != null);
            Contract.Requires(name != null);
            Contract.Requires(@value != null);
            Contract.Ensures(Contract.Result<Expression>() != null);

            var currentContextExpr = GetNthContext(0);
            var binder = ContextDeclarations[ContextDeclarations.Count - 1];

            Expression result = null;
            if (binder != null)
            {
               try
               {
                  result = binder.BindVariableWrite(thisExpr, name, @value);
               }
               catch (Exception e)
               {
                  throw SyntaxError(relativePositionInSource, e, "Failed to bind variable '{0}': {1}", name, e.Message);
               }
            }

            if (result == null)
            {
               // Try to bind a property on the current object.
               var flags = BindingFlags.Public | BindingFlags.Instance;
               if (Options.IgnoreCase)
                  flags |= BindingFlags.IgnoreCase; // todo: write test
               var fld = thisExpr.Type.GetField(name, flags | BindingFlags.SetField);
               if (fld != null)
                  return Expression.Assign(Expression.Field(thisExpr, fld), @value);
               var prop = thisExpr.Type.GetProperty(name, flags | BindingFlags.SetProperty);
               if (prop != null)
                  return Expression.Assign(Expression.Property(thisExpr, prop), @value);
            }

            if (result == null)
               throw SyntaxError(relativePositionInSource, "Cannot set variable '{0}' as the current variable binder returned null and no field or property of that name has been found on context of type '{1}'", name, thisExpr.Type.FullName);

            return result;
         }

         public Expression RemoveVariable(Expression thisExpr, String name, Int32 relativePositionInSource)
         {
            Contract.Requires(thisExpr != null);
            Contract.Requires(name != null);
            Contract.Ensures(Contract.Result<Expression>() != null);

            var currentContextExpr = GetNthContext(0);
            var binder = ContextDeclarations[ContextDeclarations.Count - 1];

            if (binder == null)
               throw this.SyntaxError(relativePositionInSource, "Cannot unset variable '{0}' because no variable binder has been set.", name);

            Expression result = null;
            try
            {
               result = binder.UnbindVariable(thisExpr, name);
            }
            catch (Exception e)
            {
               throw this.SyntaxError(relativePositionInSource, e, "Failed to unbind variable '{0}': {1}", name, e.Message);
            }

            if (result == null)
               throw this.SyntaxError(relativePositionInSource, "Cannot unset variable '{0}' because variable binder does not support it (returned null).", name);

            return result;
         }
      }
   }
}
