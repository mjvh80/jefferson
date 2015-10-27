using Jefferson.Output;
using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Reflection;

namespace Jefferson.Directives
{
   /// <summary>
   /// Throws an exception when run. Useful for enforcing e.g. invariants.
   /// </summary>
   [DebuggerDisplay("#{Name}")]
   public sealed class ErrorDirective : IDirective
   {
      private readonly ConstructorInfo _mCtor;

      public ErrorDirective() : this(typeof(Exception)) { }

      public ErrorDirective(Type exceptionType)
      {
         Contract.Requires(exceptionType != null);
         _mCtor = exceptionType.GetConstructor(new[] { typeof(String) });
         if (_mCtor == null)
            throw new InvalidOperationException("Invalid exception type: public constructor with a single string parameter expected");
      }

      public String Name
      {
         get { return "error"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         if (String.IsNullOrWhiteSpace(arguments) && String.IsNullOrWhiteSpace(source))
            throw parserContext.SyntaxError(0, "Either argument or body expected.");

         if (!String.IsNullOrWhiteSpace(arguments))
            return Expression.Throw(Expression.New(_mCtor, Expression.Constant(arguments)));

         // Compile body and that's the message.
         var sb = Expression.Variable(typeof(StringBuilderOutputWriter));
         var message = (Expression)Expression.Block(new[] { sb },
                                   Expression.Assign(sb, Expression.New(typeof(StringBuilderOutputWriter))),
                                   Expression.Invoke(parserContext.Parse<Object>(source), parserContext.GetNthContext(0), sb),
                                      Expression.Call(sb, Utils.GetMethod<StringBuilderOutputWriter>(s => s.GetOutput())));
         return Expression.Throw(Expression.New(_mCtor, message));
      }
   }
}
