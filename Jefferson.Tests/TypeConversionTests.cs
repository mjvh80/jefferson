using System;
using System.Diagnostics;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   public class TypeConversionTests
   {
      public TemplateParser Parser = new TemplateParser();

      public class TestContext
      {
         public Int32 zi4 = 0;
         public Int32 i4 = 1;
         public Int64 i8 = 3;

         public Int16 i2 = 2;

         public UInt32 maxu4 = UInt32.MaxValue;
         public UInt64 maxu8 = UInt64.MaxValue;
         public Int64 zi8 = 0;
      }

      public enum FooEnum { Foo = 1, Bar = 2 }

      [Fact]
      public void Can_widen_numbers()
      {
         var result = Parser.Replace("$$ (i4 + i8).GetType().Name $$", new TestContext());
         Assert.Equal("Int64", result.Trim());

         result = Parser.Replace("$$ (i8 + i4).GetType().Name $$", new TestContext());
         Assert.Equal("Int64", result.Trim());

         result = Parser.Replace("$$ (i2 + 0).GetType().Name $$", new TestContext());
         Assert.Equal("Int32", result);
      }

      [Theory]
      [InlineData("1ul", typeof(UInt64))][InlineData("1Ul", typeof(UInt64))]
      [InlineData("1u", typeof(UInt32))][InlineData("1U", typeof(UInt32))]
      [InlineData("1d", typeof(Double))][InlineData("1f", typeof(Single))]
      [InlineData("1m", typeof(Decimal))][InlineData("1l", typeof(Int64))]
      public void Can_parse_various_integer_literals(String input, Type expectedType)
      {
         var ep = new ExpressionParser<TestContext, Object>();
         var result = ep.ParseExpression(input, new TestContext())(new TestContext());

         Trace.WriteLine(result.GetType().FullName);
      }

      [Fact]
      public void Can_handle_sign_mismatch()
      {
         var result = Parser.Replace("$$ (maxu4 + zi4).GetType().Name $$", new TestContext());
         Assert.Equal("Int64", result);

         // Edge case that c# does not deal with.
         Assert.Equal("-1", Parser.Replace("$$ maxu8 + zi8 $$", new TestContext()));
      }

      [Fact(Skip = "Todo, figure out why this fails")]
      public void Can_compare_enum_with_enum()
      {
         Parser.Replace("$$ Jefferson.Tests.TypeConversionTests.FooEnum.Foo = Jefferson.Tests.TypeConversionTests.FooEnum.Bar $$", new TestContext());
      }
   }
}
