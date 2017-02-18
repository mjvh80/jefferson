using Jefferson.Directives;
using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Jefferson.Build.Directives
{
    [DebuggerDisplay("#{Name}")]
    public sealed class ProcessDirective : IDirective
    {
        public String Name => "process";

        public String[] ReservedWords => null;

        public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
        {
            if (source != null) throw parserContext.SyntaxError(0, "#process should not have a body");
            if (!String.IsNullOrWhiteSpace(arguments)) throw parserContext.SyntaxError(0, "#process arguments should not be empty");

            // Does nothing, this directive is just a marker that the file should be processed.
            return Expression.Default(typeof(Object)); // onp
        }
    }
}
