using Jefferson.Parsing;
using System;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public interface IDirective
   {
      /// <summary>
      /// Name of the directive, e.g. "let" for the $$#let $$ directive.
      /// </summary>
      String Name { get; }

      /// <summary>
      /// Reserved words for the parser to ignore, e.g. "out" for $$#let ..$$ ... $$#out$$.
      /// </summary>
      String[] ReservedWords { get; }

      /// <summary>
      /// Indicates that this directive is not closed, e.g. $$#define$$ is not closed by $$/define$$.
      /// </summary>
      Boolean MayBeEmpty { get; }

      Expression Compile(TemplateParserContext parserContext, String arguments, String source);
   }
}
