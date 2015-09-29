using System;
using System.Linq.Expressions;
using Xunit;

namespace Jefferson.Tests
{
   public class MiscellaneousTests
   {
      [Fact]
      public void ReplaceParameterVisitor_works()
      {
         var @params = new[] { Expression.Variable(typeof(String), "str"), Expression.Variable(typeof(Int32), "i") };
         var expr = Expression.Lambda<Action<String, Int32>>(Expression.Block(@params[0], @params[1]), @params);
         _ReplaceParameterVisitor.ReplaceParamWith(@params[0], Expression.Constant("foobar"), expr.Body);
      }

      [Fact]
      public void Ensure_works()
      {
         Assert.Throws<ArgumentNullException>(() => Ensure.NotNull(null, "foobar"));
         Assert.Throws<ArgumentException>(() => Ensure.NotNullOrEmpty("", "blah"));
         Assert.Throws<ArgumentException>(() => Ensure.NotNullOrEmpty("", null));
      }
   }
}
