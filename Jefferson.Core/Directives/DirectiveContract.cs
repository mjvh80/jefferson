using System;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Jefferson.Directives.Contracts
{
   [ContractClassFor(typeof(IDirective))]
   public abstract class DirectiveContract : IDirective
   {
      public String Name
      {
         get
         {
            Contract.Ensures(Contract.Result<String>() != null);
            Contract.Ensures(Regex.IsMatch(Contract.Result<String>(), "^[a-zA-Z]+$")); // todo bit harhs ey
            return null;
         }
      }

      public String[] ReservedWords
      {
         get
         {
            Contract.Ensures(Contract.Result<String[]>() == null || Contract.ForAll(Contract.Result<String[]>(), s => Regex.IsMatch(s, "^[a-zA-Z]+$"))); // todo: as above
            return null;
         }
      }

      public Boolean MayBeEmpty
      {
         get { return false; }
      }

      public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         Contract.Requires(parserContext != null);
         Contract.Requires(MayBeEmpty || source != null);
         Contract.Ensures(Contract.Result<Expression>() != null);
         return null;
      }
   }
}
