using System;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;
using Jefferson.Extensions;
using System.Linq;
using Xunit.Sdk;
using Xunit.Extensions;

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

      [Fact]
      public void Utils_debug_asserts_work()
      {
#if DEBUG
         Assert.Throws<TraceAssertException>(() => Utils.DebugAssert(false));
         Assert.Throws<TraceAssertException>(() => Utils.DebugAssert(false, "foo"));
         Assert.Throws<TraceAssertException>(() => Utils.AssertNotNull(null, null));
         Assert.Throws<TraceAssertException>(() => Utils.AssertNotNull(null, "oue"));
#endif
         Utils.DebugAssert(true);
         Utils.DebugAssert(true, "aoue");
         Utils.AssertNotNull("oue");
         Utils.AssertNotNull("oue", "oeu");
      }

      private static void _Void() { }

      [Fact]
      public void Can_get_constructors_with_utils()
      {
         Assert.Throws<InvalidOperationException>(() => Utils.GetConstructor(() => _Void()));
      }

      [Fact]
      public void Some_other_utils_tests()
      {
         Assert.Throws<Exception>(() => Utils.FindTypeInAppDomain("aoeueuaoueaoueaoue", true));
      }

      [Fact]
      public void SyntaxException_tests()
      {
         SyntaxException.Create(new Exception(), null, null);
         SyntaxException.Create(null, "foo {0}", "bar");
      }

      [Fact]
      public void Extensions_ToStringInvariant_work()
      {
         foreach(var m in typeof(Extensions._Extensions).GetMethods(BindingFlags.Static | BindingFlags.Public).Where(m => m.Name == "ToStringInvariant"))
         {
            var p = m.GetParameters()[0];
            if (p.ParameterType.IsGenericType)
            {
               // Nullable, test it with null.
               m.Invoke(null, new Object[] { null });
               Assert.True(p.ParameterType.GetGenericArguments()[0].IsValueType);
               m.Invoke(null, new Object[] { Activator.CreateInstance(p.ParameterType.GetGenericArguments()[0]) });
            }
            else
            {
               Assert.True(p.ParameterType.IsValueType);
               m.Invoke(null, new Object[] { Activator.CreateInstance(p.ParameterType) });
            }
         }
      }

      [Theory]
      [InlineData("", 0, -1)][InlineData("  ", 0, 0)][InlineData(" ", -1, -1)]
      [InlineData(" ", 10, -1)][InlineData("foo bar", 1, 3)][InlineData("foobar\r", 3, 6)]
      public void Can_find_whitespace(String str, Int32 startAt, Int32 expectedIndex)
      {
         var i = str.IndexOfWhiteSpace(startAt);
         Assert.Equal(expectedIndex, i);
      }
   }
}
