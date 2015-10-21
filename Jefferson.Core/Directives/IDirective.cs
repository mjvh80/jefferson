using Jefferson.Parsing;
using System;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   [ContractClass(typeof(Contracts.DirectiveContract))]
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
      /// Compile the directive given the parser context, its arguments and body source. For any directive
      /// A in $$#A arguments$$ source $$/A$$.
      /// </summary>
      Expression Compile(TemplateParserContext parserContext, String arguments, String source);
   }
}
