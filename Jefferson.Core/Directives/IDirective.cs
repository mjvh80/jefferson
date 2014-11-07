using Jefferson.Parsing;
using System;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public interface IDirective
   {
      String Name { get; }
      String[] ReservedWords { get; }

      Expression Compile(TemplateParserContext parserContext, String arguments, String source);
   }
}
