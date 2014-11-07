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
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

// Todo:
// - nuget pkg building
// - look at using dynamic

namespace Jefferson
{
   /// <summary>
   /// Parses a template and returns a lambda expression accepting an object and an outputwriter as input.
   /// </summary>
   public class TemplateParser
   {
      /// <summary>
      /// Creates a parser with default directives $$#if and $$#each registered.
      /// </summary>
      public TemplateParser() : this(new IfDirective(), new EachDirective()) { }

      /// <summary>
      /// Creates a parser with the given directives.
      /// </summary>
      public TemplateParser(params IDirective[] directives)
      {
         var reservedWords = new HashSet<String>();
         _mDirectiveMap = new Dictionary<String, IDirective>(directives.Length);
         foreach (var directive in directives.Where(d => d != null))
         {
            if (directive.Name == null) throw Utils.Error("Invalid directive: null name");

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
      }

      private readonly Dictionary<String, IDirective> _mDirectiveMap;
      private static readonly Regex _sDirectiveNameExpr = new Regex("^[a-zA-Z]+$"); // for now

      /// <summary>
      /// Convenience method. Calls Parse and compiles the resulting expression tree.
      /// </summary>
      public Action<TContext, IOutputWriter> Compile<TContext>(String source, Type contextType = null, IVariableDeclaration decls = null, Boolean except = false)
      {
         return Parse<TContext>(source, contextType, decls, except).Compile();
      }

      /// <summary>
      /// Parse the given source into an expression tree.
      /// </summary>
      /// <typeparam name="TContext">The type of the input value. Note that if contextType != typeof(TContext) then contextType must be a subclass of the context.</typeparam>
      /// <param name="source">Source code in which occurrences of template markers are replaced.</param>
      /// <param name="contextType">The actual type of the input value.</param>
      /// <param name="decls">Variable declartions, see <see cref="IVariableDeclaration"/></param>
      /// <param name="except">If false, the parser won't throw if a variable is not declared. The empty string is used then.</param>
      public Expression<Action<TContext, IOutputWriter>> Parse<TContext>(String source, Type contextType = null, IVariableDeclaration decls = null, Boolean except = false)
      {
         if (contextType == null) contextType = typeof(TContext);

         var ctx = new TemplateParserContext(source, except)
         {
            ContextTypes = new List<Type> { contextType },
            ContextDeclarations = new List<IVariableDeclaration> { decls },
            DirectiveMap = _mDirectiveMap,
            PositionOffsets = new Stack<Int32>()
         };

         return ctx.Parse<TContext>(source);
      }

      /// <summary>
      /// Compiles and runs the given source and input context. In effect this interprets the source codes rather than compiling it.
      /// </summary>
      public String Replace(String source, Object context, Boolean except = false)
      {
         Ensure.NotNull(context);

         var tree = Parse<Object>(source, context.GetType(), context as IVariableDeclaration, except);
         var buffer = new StringBuilder();

         tree.Compile()(context, new StringBuilderOutputWriter(buffer));
         return buffer.ToString();
      }

      /// <summary>
      /// Keeps replacing expressions until none found.
      /// </summary>
      public String ReplaceDeep(String source, Object context, Boolean except = false)
      {
         var loop = 1;
         do
         {
            // If you need more than 1000 iterations, just do as this code without the loop "detection".
            if (loop > 1000) // say
               throw Utils.Error("Possible loop detected in ReplaceDeep, stopping after 1000 iterations.");

            source = Replace(source, context, except);
            loop += 1;
         }
         while (source.IndexOf("$$") >= 0);

         return source;
      }
   }

   namespace Parsing
   {
      /// <summary>
      /// Represents state of the parser during parsing.
      /// </summary>
      public class TemplateParserContext
      {
         public TemplateParserContext(String source, Boolean except)
         {
            Ensure.NotNull(source);
            Source = source;
            ShouldThrow = except;
            Output = Expression.Parameter(typeof(IOutputWriter), "output");
         }

         internal Dictionary<String, IDirective> DirectiveMap;

         internal List<Type> ContextTypes;
         internal List<IVariableDeclaration> ContextDeclarations;
         internal Stack<Int32> PositionOffsets;

         /// <summary>
         /// Represents the runtime List&lt;Object&gt;, the stack of current contexts (or scopes).
         /// This is a list because we need random access to it sometimes.
         /// </summary>
         public ParameterExpression RuntimeContexts { get; private set; }

         /// <summary>
         /// The parameter representing an instance of <see cref="IOutputWriter"/> used for output.
         /// </summary>
         public ParameterExpression Output { get; private set; }

         /// <summary>
         /// Set to true if the except parameter of Parse was set to true, and vice versa.
         /// </summary>
         public readonly Boolean ShouldThrow;

         /// <summary>
         /// The global source being parsed.
         /// </summary>
         public readonly String Source;

         /// <summary>
         /// Pushes a scope which is a Type for the current scope/context and variable declarations for this scope.
         /// </summary>
         public void PushScope(Type context, IVariableDeclaration variables = null)
         {
            Ensure.NotNull(context); // variables may be null, i.e. no new variables introduced
            ContextTypes.Add(context);
            ContextDeclarations.Add(variables);
         }

         /// <summary>
         /// Pops the current scope, see <see cref="PushScope"/>.
         /// </summary>
         public void PopScope()
         {
            var count = ContextTypes.Count;
            Utils.DebugAssert(count == ContextDeclarations.Count);
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
         public IVariableDeclaration CurrentVariableDeclaration
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
         public Int32 GetPosition(Int32 position)
         {
            return CurrentPositionOffset + position;
         }

         /// <summary>
         /// Get an expression that, at runtime, gets the nth context. The 0th context is the current context, the 1st context
         /// is its parent and so on. This corresponds to $x expressions where $0 is the 0th context.
         /// </summary
         public Expression GetNthContext(Int32 n)
         {
            Utils.DebugAssert(RuntimeContexts.Type == typeof(List<Object>));

            var indexer = typeof(List<Object>).GetProperty("Item");
            Utils.AssertNotNull(indexer);

            var idx = ContextTypes.Count - 1 - n;
            return Expression.Convert(Expression.MakeIndex(RuntimeContexts, indexer, new[] { Expression.Constant(idx) }),
                                      ContextTypes[idx]);
         }

         /// <summary>
         /// Throws a <see cref="SyntaxException"/> by also translating the position to the global source.
         /// </summary>
         public Exception SyntaxError(Int32 relativeIdx, String msg, params Object[] args)
         {
            return SyntaxException.Create(this.Source, GetPosition(relativeIdx), msg, args);
         }

         /// <summary>
         /// Beef.
         /// </summary>
         public Expression<Action<TContext, IOutputWriter>> Parse<TContext>(String source)
         {
            var except = this.ShouldThrow;
            if (!(typeof(TContext).IsAssignableFrom(CurrentContextType)))
               throw Utils.InvalidOperation("Invalid Compile call, generic argument type '{0}' is not a baseclass for current context type '{1}'.", typeof(TContext).FullName, CurrentContextType.FullName);

            // See if we can find jefferson.
            var idx = source.IndexOf("$$", 0, StringComparison.Ordinal);
            if (idx < 0) return (c, sb) => sb.Write(source);

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

            var prevIdx = 0;
            while (idx >= 0 && idx < source.Length)
            {
               var chunk = source.Substring(prevIdx, idx - prevIdx);
               bodyStmts.Add(Expression.Call(outputParam, writeMethod, Expression.Constant(chunk)));

               String expression = null;
               if (idx < source.Length - 6 && source[idx + 2] == '#')
               {
                  // We are parsing a directive.
                  // Find the name of the directive, and pass on control.
                  var dirNameEndIdx = Utils.MinNonNeg(source.IndexOf(' ', idx + 3), source.IndexOf("$$", idx + 3));
                  if (dirNameEndIdx < 0) throw SyntaxError(idx, "Could not find end of directive.");

                  var dirBodyStartIdx = source.IndexOf("$$", idx + 3);
                  if (dirBodyStartIdx < 0) throw SyntaxError(idx, "Could not find end of directive.");
                  dirBodyStartIdx += 2; // skip $$

                  var directiveName = source.Substring(idx + 3, dirNameEndIdx - idx - 3);
                  IDirective directive;
                  if (!DirectiveMap.TryGetValue(directiveName, out directive))
                     throw SyntaxError(idx, "Could not find directive '{0}'.", directiveName);

                  var directiveEnd = "$$/" + directiveName + "$$";
                  var directiveEndIdx = FindDirectiveEnd(source, dirNameEndIdx, directiveEnd);
                  if (directiveEndIdx < 0) throw SyntaxError(idx, "Failed to find directive end '{0}' for directive '{1}'.", directiveEnd, directiveName);
                  if (dirBodyStartIdx >= directiveEndIdx) throw SyntaxError(idx, "Could not find end of directive.");

                  // Mark where to continue parsing.
                  prevIdx = directiveEndIdx + directiveEnd.Length;

                  // Add the compiled directive to the body.
                  // Keep track of line numbers in global source.
                  PositionOffsets.Push(CurrentPositionOffset + dirBodyStartIdx);

                  bodyStmts.Add(directive.Compile(this, arguments: source.Substring(dirNameEndIdx, dirBodyStartIdx - dirNameEndIdx - 2),
                                                        source: source.Substring(dirBodyStartIdx, directiveEndIdx - dirBodyStartIdx)));

                  PositionOffsets.Pop();
               }
               else
               {
                  // Simpler replacement expression.
                  var closeIdx = source.IndexOf("$$", idx + 2);
                  if (closeIdx < 0)
                     throw SyntaxError(idx, "Found $$ but could not find matching end.");

                  expression = source.Substring(idx + 2, closeIdx - idx - 2);
                  prevIdx = closeIdx + 2;
               }

               if (expression != null)
               {
                  var compExpr = EvaluateExpression<Object>(expression, except).Ast;

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
               bodyStmts.Add(Expression.Call(outputParam, writeMethod, Expression.Constant(lastChunk)));
            }

            // Construct the final expression.
            return Expression.Lambda<Action<TContext, IOutputWriter>>(Expression.Block(new[] { runtimeCtxs }, bodyStmts), contextParam, outputParam);
         }

         /// <summary>
         /// Finds the given "block" terminators, e.g. for #if one wants to look for $$#else$$, $$#elif$$ or $$/if$$.
         /// This is a convenience method that can handle nested directives.
         /// </summary>
         public Int32 FindDirectiveEnd(String source, Int32 start, params String[] terminators)
         {
            // Find the next directive.
            // If we see something like #else it is not a directive because it doesn't have a matching /else.
            var nestedStartIdx = -1;
            var nestedAfterEndIdx = -1;
            for (var nextStart = start; nestedAfterEndIdx < 0; )
            {
               nestedStartIdx = source.IndexOf("$$#", nextStart);
               if (nestedStartIdx < 0) break;

               nextStart = source.IndexOf("$$", nestedStartIdx + 3);
               if (nextStart < 0) throw SyntaxError(nestedStartIdx, "Could not find matching $$.");
               nextStart += 2;

               var dirIdx = source.IndexOf(" ", nestedStartIdx + "$$#".Length); // look for $$#<word><space>...$$
               if (dirIdx < 0 || dirIdx > nextStart - 2) // or $$#<word>$$
                  dirIdx = nextStart - 2;

               var len = dirIdx - nestedStartIdx - "$$#".Length;
               if (len == 0) throw SyntaxError(nestedStartIdx, "Invalid empty directive found.");

               var nestedDirective = source.Substring(nestedStartIdx + "$$#".Length, len);
               nestedDirective = "$$/" + nestedDirective + "$$";

               nestedAfterEndIdx = FindDirectiveEnd(source, nextStart + 2, nestedDirective);
               if (nestedAfterEndIdx >= 0)
                  nestedAfterEndIdx += nestedDirective.Length;
            }

            // Find the first terminator.
            var endIdx = Utils.MinNonNeg(terminators.Select(t => source.IndexOf(t, start)));

            if (nestedStartIdx < 0) return endIdx; // no nested directive
            if (endIdx < 0) return endIdx; // error: not found

            if (endIdx <= nestedStartIdx) return endIdx; // not nested

            var endOfStart = source.IndexOf("$$", nestedStartIdx + "$$#".Length);
            if (endOfStart < 0) throw SyntaxError(nestedStartIdx, "Could not find ending $$ for directive starting.");

            // Restart search after nested directive.
            if (nestedAfterEndIdx < 0) return -1; // got nowehere else to go

            return FindDirectiveEnd(source, nestedAfterEndIdx, terminators);
         }

         public CompiledExpression<Object, TOutput> EvaluateExpression<TOutput>(String expr, Boolean except)
         {
            var parser = new _ExpressionParser<Object, TOutput>();

            // Parse the expression, compile it and run it.
            // todo: flags
            return parser._ParseExpressionInternal(expr, ResolveName, _ExpressionParsingFlags.IgnoreCase, this.CurrentContextType);
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
         private Expression ResolveName(Expression thisExpr, String name, String typeName, NameResolverDelegate @base)
         {
            Utils.DebugAssert(thisExpr.Type == GetNthContext(0).Type);

            var startIndex = 0;

            if (typeName != null)
            {
               if (_sParentExpr.IsMatch(typeName))
               {
                  startIndex = Int32.Parse(typeName.Substring(1));

                  // special name which moves up the context stack.
                  if (startIndex > ContextTypes.Count - 1)
                     throw SyntaxException.Create("Invalid parent context '{0}': number of contexts in chain is {1}", typeName, ContextTypes.Count.ToStringInvariant());
               }
               else
                  // Default lookup.
                  return @base(thisExpr, name, typeName, @base);
            }

            // Resolve up till root context.
            for (var startContext = GetNthContext(startIndex); ; )
            {
               var baseResolve = @base(startContext, name, typeName, null);
               if (baseResolve != null) return baseResolve;

               // See if a variable has been declared.
               var decls = ContextDeclarations[ContextDeclarations.Count - 1 - startIndex];

               var varType = decls == null ? null : decls.GetType(name);
               if (varType != null)
               {
                  var indexer = startContext.Type.GetProperty("Item");
                  if (indexer == null)
                     throw SyntaxException.Create("Context type '{0}' declares variables but provides no indexer to obtain them.", startContext.Type.FullName);

                  return Expression.Convert(Expression.MakeIndex(startContext, indexer, new[] { Expression.Constant(name) }), varType);
               }

               startIndex += 1;
               if (startIndex == ContextTypes.Count) break;
            }

            // If here, cannot resolve value.
            if (ShouldThrow) return null;
            else return Expression.Constant(""); // default empty string value
         }
      }
   }
}
