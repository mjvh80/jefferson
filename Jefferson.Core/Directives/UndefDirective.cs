using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public sealed class UndefDirective : IDirective
   {
      public String Name
      {
         get { return "undef"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         var args = arguments.Split(new[] { ';' }).Select(n => n.Trim()).Where(n => n.Length > 0);
         foreach (var name in args)
            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(0, "Variable '{0}' has an invalid name.", name);

         var body = new List<Expression>(args.Select(name => parserContext.RemoveVariable(parserContext.GetNthContext(0), name, 0))); // todo track position of names for somewhat better errors

         if (body.Count == 0)
            throw parserContext.SyntaxError(0, "#undef requires arguments (variables to undefine)");

         return Expression.Block(body);
      }
   }
}
