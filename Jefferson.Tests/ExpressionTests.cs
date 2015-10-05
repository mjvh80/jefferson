using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   #region Test Helper Classes

   public class Foo
   {
      public String Bar = "bar";

      public String this[String idx] { get { return "foo"; } }

      public String Bar2() { return "w00t"; }
      public Func<String> Bar3 = () => "bar3";

      public Func<Func<String>> Complex = () => () => "complex";

      public class BarNested
      {
         public static String Baz = "Bazzooba";
      }
   }

   public enum Flags
   {
      None = 0,
      Foo = 1,
      Bar = 2
   }

   public class Context
   {
      public Context()
      {
         NV = new NameValueCollection();
         NV["foo"] = "bar";
      }

      public Object APPROOT = @"C:\foo\bar";

      public NameValueCollection NV;

      public Foo Foo = new Foo();
      public String Foobar = "foobar";

      public Int32 Thrifty = 30;

      public String and = "and";

      public Double Dbl = 2.0;

      public Int32 One = 1;

      public Int32[] Ar = new[] { 10, 20, 30, 1 };

      public Int32[][] Ar2 = new[] { new[] { 0 }, new[] { 1, 2 } };

      public Object ImNull = null;

      public String Name2 = "name2";

      public String NullString = null;
      public Object NullObject = null;

      public Double e = 0.0;

      public String JoinPath(String d, String f) { return Path.Combine(d, f); }
      public String IncludeFile(String f) { return f; }

      public String FuncTakingBool(Boolean b) { return b.ToString(); }

      public Object Obj = new Object();

      public Func<String> Funcy = () => "foobar";

      public String Echo(String one, String two)
      {
         return one + two;
      }

      public String EchoOne(String one) { return one; }

      public Object BoxedTrue = true;
      public Object BoxedFalse = false;
      public Object StrAsObj = "";
      public Int32 BoxedInt = 7;
      public Int32 BoxedZeroInt = 0;

      public bool Regex(Regex r, String str)
      {
         return r.IsMatch(str);
      }

      public String this[Int32 i] { get { return (-i).ToString(); } }
      public String this[Int32 i, Int32 j] { get { return (i + j).ToString(); } }

      public static Boolean StaticBool() { return true; }
      public static Boolean StaticFld = true;
   }

   public class DirectInheritedContext : Context
   {
      public String FooDirect = "Direct Foo";

      public String DirectFoo() { return "DF"; }
   }

   public class IndirectInheritedContext : DirectInheritedContext
   {
      public String FooIndirect = "Indirect Foo";

      public String IndirectFoo() { return "IDF"; }
   }

   #endregion

   public class ExpressionParserTests
   {
      public ExpressionParserTests()
      { }

      public class DebuggerViewTest
      {
         public String Name = "Hey there";
         public String foobar(Int32 i) { return "foobar"; }
      }


      public class ActualContext : Context
      {
         public String Actual = "ACTUAL";
      }

      [Fact]
      public void Can_work_with_enums_and_flags()
      {
         var ns = typeof(Flags).Namespace;
         Assert.True(_ParseAndRun(ns + ".Flags.Foo == 1"));

         // Test for the presence of flags.
         Assert.True(_ParseAndRun("1 & " + ns + ".Flags.Foo"));
         Assert.True(_ParseAndRun("-1 # " + ns + ".Flags.Foo"));
      }

      [Fact]
      public void All_overloaded_constructors_work()
      {
         var p = new ExpressionParser<Context, String>();
         p.ParseExpression("Actual", new ActualContext());

         TestUtils.AssertThrowsContractException(() => p.ParseExpression("Actual", (ActualContext)null));

         p.ParseExpression<ActualContext>("Actual");
      }

      [Fact]
      public void Actual_context_type_is_verified()
      {
         var p = new ExpressionParser<Context, String>();
         Assert.Throws<SyntaxException>(() => p.ParseExpression("Actual", "i'm not of the right type"));
      }

      [Fact]
      public void TryParseExpression_works()
      {
         var p = new ExpressionParser<ActualContext, String>();

         ExpressionDelegate<ActualContext, String> result;
         Assert.True(p.TryParseExpression("Actual = 'ACTUAL'", out result));
         Assert.Equal("True", result(new ActualContext()));

         Assert.False(p.TryParseExpression("Actualeee", out result));
         Assert.Null(result);
      }

      //[Fact]
      //public void Foobar()
      //{
      //   String x = "foo";
      //   String y = "foo";
      //   Object z = "foo";

      //   Trace.WriteLine(x == y);
      //   Trace.WriteLine(x == z);
      //}

      [Fact]
      public void If_expressions_are_supported()
      {
         var p = new ExpressionParser<Context, String>();
         var c = new Context();

         Assert.Equal("boe", p.ParseExpression("if(true)'boe'")(c));
         Assert.Equal("boe", p.ParseExpression("if true 'boe'")(c));

         Assert.Equal("", p.ParseExpression("if(false)'boe'")(c) ?? "");
         Assert.Equal("baz", p.ParseExpression("if(false)'boe'else'baz'")(c));

         // Can handle non-bools.
         Assert.Equal("bazz", p.ParseExpression("if '' 'foo' else 'bazz'")(c));
      }

      [Fact]
      public void Issue_with_method_param_coercion()
      {
         var p = new ExpressionParser<Context, String>();

         var error = Assert.Throws<SyntaxException>(() => p.ParseExpression("IncludeFile(JoinPath(APPROOT.ToString(), 'site\\packages.config'))", (@this, n, tn, def) =>
         {
            // Test: always return something not-null (as-if resolved dynamically at run-time).
            if (tn != null) return def(@this, n, tn, null);
            return Expression.Constant("Foobar");
         })(new Context()));

         // As our resolver takes precendence.
         //   Assert.Equal(@"Foobar\site\packages.config", c(new Context()));

         // Behaviour here changed. If we *override* resolution for a name, that always takes precendence.
         // Our "binder" is the default rather than the norm.
         Assert.Contains("Expected 'JoinPath' to resolve to a delegate or method", error.Message);
      }

      public class TypeCoercions
      {
         public String Foo(String x) { return x; }

         public Int16 Short(Int16 s) { return s; }

         public String Foobar(Int32 x) { return x.ToString(); }
         public String Foobar(String x) { return x; }
         public String Foobar(Int16 x) { return x.ToString(); }

         public String Obj(Object o) { return o == null ? "null" : o.ToString(); }
      }

      public class TypeCoercionsEx : TypeCoercions
      {

      }

      [Fact]
      public void Various_type_coercion_tests()
      {
         var p = new ExpressionParser<TypeCoercions, String>();
         var c = new TypeCoercions();
         Func<String, String> run = s => p.ParseExpression(s)(c);

         Assert.Equal("1", run("Foo(1)"));
         Assert.Equal("1.2", run("Foo(1.2)"));
         Assert.Equal("test", run("Foo(/test/)"));
         Assert.Equal("12", run("Short(12)"));
         Assert.Equal("null", run("Obj(null)"));
         Assert.Equal("foo", run("Obj('foo')"));
         Assert.Equal("1", run("Obj(1)"));

         // todo: more and more tests here
      }

      [Fact]
      public void Can_call_inherited_method()
      {
         var p = new ExpressionParser<TypeCoercionsEx, String>();
         var c = new TypeCoercionsEx();
         Func<String, String> run = s => p.ParseExpression(s)(c);

         Assert.Equal("1", run("Foo(1)"));
      }

      [Fact]
      public void Can_ignore_method_case()
      {
         var p = new ExpressionParser<TypeCoercions, String>();
         var c = new TypeCoercions();

         Assert.Throws<SyntaxException>(() => p.ParseExpression("foo(1)")(c)); // can ignore case

         var result = p.ParseExpression("foo(1)", null, ExpressionParsingFlags.IgnoreCase)(c);
         Assert.Equal("1", result);
      }

      [Fact]
      public void Cannot_handle_overloading_in_some_cases()
      {
         var p = new ExpressionParser<TypeCoercions, String>();
         var c = new TypeCoercions();
         Func<String, String> run = s => p.ParseExpression(s)(c);

         // todo: should we attempt to solve these situations?
         var error = Assert.Throws<SyntaxException>(() => run("Foobar(1L)")); // NOTE the Int64 -> cannot choose between two overloads which both convert
         Assert.Contains("Ambiguous method call", error.Message);
      }

      [Fact]
      public void Can_handle_null_in_match_input()
      {
         var parser = new PredicateParser<Context>();
         var result = parser.ParseExpression("NullString =~ /foobar/i")(new Context());
         Assert.False(result);

         // Note, however, that null matches "nothing".
         result = parser.ParseExpression("NullString =~ /(?:)/i")(new Context());
         Assert.True(result);
      }

      [Fact]
      public void Empty_regex_is_not_valid_as_it_denotes_a_comment()
      {
         // This is the same behaviour as seen in e.g. JavaScript.

         var parser = new PredicateParser<Context>();
         Assert.Throws<SyntaxException>(() => parser.ParseExpression("NullString =~ //i")(new Context()));
      }

      [Theory]
      [InlineData(@"1//")]
      [InlineData("1   //")]
      [InlineData("1  //   ")]
      [InlineData(@" 1//")]
      [InlineData(" 1   //")]
      [InlineData(" 1  //   ")]
      [InlineData(@"1 + // foobar
                    2// blah ")]
      public void Can_skip_comments_in_expressions(String source)
      {
         var p = new PredicateParser<Context>();
         var result = p.ParseExpression(source)(new Context());
      }

      [Fact]
      public void Can_resolve_namespaces_etc()
      {
         Assert.True(_ParseAndRun("StaticFld"));
         Assert.True(_ParseAndRun(typeof(Context).Namespace + ".Context.StaticFld"));
      }

      [Fact]
      public void Can_use_derived_context_type()
      {
         PredicateParser<Context> parser = new PredicateParser<Context>();
         var func = parser.ParseExpression("Actual == 'ACTUAL'", typeof(ActualContext));
         Assert.True(func(new ActualContext()));

         Assert.Throws<InvalidCastException>(() => func(new Context()));
      }

      [Theory]
      [InlineData("valid", true)]
      [InlineData(null, false), InlineData("", false), InlineData(" ", false), InlineData("\t", false), InlineData("\n", false)]
      [InlineData("1foo", false), InlineData("1", false)]
      [InlineData("foobar", true), InlineData("$foobar", true), InlineData("_foobar", true), InlineData("foo_bar$e", true)]
      public void IsValidName_works(String name, Boolean isValid)
      {
         Assert.Equal(isValid, ExpressionParser<Object, Object>.IsValidName(name));
      }

      [Fact]
      public void String_concatenation_using_anonymous_object()
      {
         var anonymousObject = new { One = 1, Str = "String" };
         var parser = new ExpressionParser<Object, String>();
         var tRunner = parser.ParseExpression("One + Str + One", anonymousObject.GetType());
         Assert.Equal("1__1", tRunner(new { One = 1, Str = "__" }));
      }

      [Fact]
      public void Can_use_anonymous_context()
      {
         var anonymousObject = new { Name = "name", Surname = "foobar" };
         var parser = new ExpressionParser<Object, String>();
         var runner = parser.ParseExpression("Name + ' ' + Surname", anonymousObject.GetType());
         Assert.Equal("Marcus van Houdt", runner(new { Name = "Marcus", Surname = "van Houdt" }));
      }

      [Fact]
      public void Inheritance_works()
      {
         var parser = new ExpressionParser<Context, String>();

         Assert.Equal("Direct Foo", parser.ParseExpression("FooDirect", typeof(DirectInheritedContext))(new DirectInheritedContext()));
         Assert.Equal("Direct Foo", parser.ParseExpression("FooDirect", typeof(DirectInheritedContext))(new IndirectInheritedContext()));

         Assert.Equal("DF", parser.ParseExpression("DirectFoo()", typeof(DirectInheritedContext))(new DirectInheritedContext()));
         Assert.Equal("DF", parser.ParseExpression("DirectFoo()", typeof(DirectInheritedContext))(new IndirectInheritedContext()));

         Assert.Equal("IDF", parser.ParseExpression("IndirectFoo()", typeof(IndirectInheritedContext))(new IndirectInheritedContext()));
      }

      [Fact]
      public void Various_tests()
      {
         PredicateParser<Context> tParser = new PredicateParser<Context>();

         Assert.True(tParser.ParseExpression(" this . Regex ( /f../ , 'f00' ) ")(new Context()));

         Assert.True(tParser.ParseExpression("Regex(/f../, 'f00')")(new Context()));
         Assert.True(tParser.ParseExpression("Regex(/f../ixs , 'f00')")(new Context()));
         Assert.True(tParser.ParseExpression(" this . Regex ( /f../ , 'f00' ) ")(new Context()));

         Assert.True(tParser.ParseExpression("Funcy() == 'foobar'")(new Context()));
         Assert.True(tParser.ParseExpression("Foo.Bar2() == 'w00t'")(new Context()));
         Assert.True(tParser.ParseExpression("Foo.Bar3() == 'bar3'")(new Context()));
         Assert.True(tParser.ParseExpression("Foo.Complex()() == 'complex'")(new Context()));
         Assert.True(tParser.ParseExpression("Foo.Complex ( ) (   ) == 'complex'")(new Context()));

         Assert.True(tParser.ParseExpression("'foobar' == Echo( 'foo' , 'bar')")(new Context()));
         Assert.True(tParser.ParseExpression("'foobar' == Echo( 'foo' , 'bar' )  ")(new Context()));
         Assert.True(tParser.ParseExpression("'foobar' == this.Echo  ( 'foo' , 'bar' )")(new Context()));
         Assert.True(tParser.ParseExpression("'foobar' == this . Echo  ( 'foo' , 'bar' ) ")(new Context()));
      }

      [Fact]
      public void Indexing_works()
      {
         PredicateParser<Context> tParser = new PredicateParser<Context>();
         Assert.True(tParser.ParseExpression("this.Ar2[1][1] == 2")(new Context()));
         Assert.False(tParser.ParseExpression(" this . Ar2 [ 1 ] [ 1 ]  == 3 ")(new Context()));

         // Can index a string. Note that our language has no char support.
         Assert.True(tParser.ParseExpression("'foobar'[(2 - 1)] == 'o'")(new Context()));

         // Can index using this.
         Assert.True(_ParseAndRun("this[1]"));
      }

      public class UnderScoreTestContext
      {
         public String Foo_Bar = "foobar";
      }

      [Fact]
      public void Null_ref_issue()
      {
         var flags = ExpressionParsingFlags.AddPdbGenerator;
         var p = new PredicateParser<Context>();

         // Interesting special case. The conversion that happens here is to string, not boolean!
         Assert.False(_ParseAndRun("'' == false"));

         Assert.False(_ParseAndRun("null == 3"));
         Assert.False(p.ParseExpression("null == 'true'", null, flags)(new Context()));
         Assert.True(_ParseAndRun("null == false")); // i think this should work

         Assert.True(_ParseAndRun("NullString == NullObject"));
         Assert.True(_ParseAndRun("NullObject == false"));
         Assert.True(_ParseAndRun("null == NullObject"));
         Assert.True(_ParseAndRun("NullObject = null"));

         Assert.True(_ParseAndRun("4 == true"));
         Assert.True(_ParseAndRun("4 == 4.0"));
      }

      [Fact]
      public void Can_handle_undersore_in_identifiers()
      {
         var parser = new ExpressionParser<UnderScoreTestContext, String>().ParseExpression("foo_bar", null, ExpressionParsingFlags.IgnoreCase, null);
         var value = parser(new UnderScoreTestContext());
         Assert.Equal("foobar", value);
      }

      [Fact]
      public void Ternary_operators_work()
      {
         var p = new ExpressionParser<Context, String>();

         Assert.Equal("foo", p.ParseExpression("true ? 'foo' : 'bar'")(new Context()));
         Assert.Equal("bar", p.ParseExpression("false ? 'foo' : 'bar'")(new Context()));
         Assert.Equal("baz", p.ParseExpression("'' ? 'foo' : 'baz'")(new Context()));
      }

      [Fact]
      public void Positional_errors_are_fairly_accurate()
      {
         var ex = SyntaxException.Create("foobar", 0);
         Assert.Equal(
@"Error parsing expression

1: foobar
   ^ (1)", ex.Message);

         ex = SyntaxException.Create("foobar", 2);
         Assert.Equal(
@"Error parsing expression

1: foobar
     ^ (3)", ex.Message);

         ex = SyntaxException.Create("foobar", 3, "WOO foo {0}", "bar");
         Assert.Equal(
@"WOO foo bar

1: foobar
      ^ (4)", ex.Message);

         ex = SyntaxException.Create("foobar", 10, "WOO foo {0}", "bar");
         Assert.Equal(
@"WOO foo bar

1: foobar
             ^ (11)", ex.Message);
      }

      [Fact]
      public void Identifiers_may_contain_dollar_signs()
      {
         var context = new Context();
         context.NV.Add("$Foo$Bar$", "xuq");
         var parser = new PredicateParser<Context>();
         NameResolverDelegate resolver = (e, n, tn, b) => Expression.Constant(context.NV[n]);
         Assert.True(parser.ParseExpression("$Foo$Bar$ == 'x' + 'uq'", resolver)(context));
      }

      [Fact]
      public void Non_string_results_are_converted()
      {
         var parser = new ExpressionParser<Context, String>();

         var result = parser.ParseExpression("true && true")(new Context());

         Assert.Equal("True", result);
         Assert.NotEqual("true", result);
      }

      [Fact]
      public void Regex_tests()
      {
         var parser = new PredicateParser<Context>();

         // There's no implicit conversion between chars and strings.
         //  Assert.Equal(true, tParser.ParseExpression("'foo'[0] == 'f'")(new Context()));

         Assert.True(parser.ParseExpression("'foo' =~ /foo/")(new Context()));
         Assert.True(parser.ParseExpression(@"'fo/o' =~ /fo\/o/")(new Context()));
         Assert.True(parser.ParseExpression(@"'Name2' =~ /^.ame\d$/")(new Context()));

         Assert.False(parser.ParseExpression("'BAR' =~ /bar/")(new Context()));
         Assert.True(parser.ParseExpression("'BAr' =~ /baR/i")(new Context()));
         Assert.True(parser.ParseExpression("'foo' =~ /   foo  /x")(new Context()));
         Assert.False(parser.ParseExpression("'bar' =~ /foo/")(new Context()));
         Assert.True(parser.ParseExpression("'\nfoo' =~ /.foo/s")(new Context())); // test the new line
         Assert.False(parser.ParseExpression("'\nfoo' =~ /.foo/")(new Context())); // test the new line
         Assert.True(parser.ParseExpression("'\nfoo' !~ /.foo/")(new Context())); // test the new line

         Assert.True(parser.ParseExpression("'foo\nbar' =~ /  foo  . \n BAR /sxi")(new Context()));
      }

      [Fact]
      public void Can_handle_non_default_cultures()
      {
         var currentCulture = Thread.CurrentThread.CurrentCulture;
         try
         {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
            General_expression_tests();
         }
         finally
         {
            Thread.CurrentThread.CurrentCulture = currentCulture;
         }
      }

      [Fact]
      public void Bool_conversions_work()
      {
         var parser = new PredicateParser<Context>();
         var c = new Context();

         // Truthies
         Assert.True(parser.ParseExpression("Foo")(c));
         Assert.True(parser.ParseExpression("1")(c));
         Assert.True(parser.ParseExpression("2")(c));
         Assert.True(parser.ParseExpression("2.0")(c));
         Assert.True(parser.ParseExpression("true")(c));
         Assert.True(parser.ParseExpression("/foo/")(c));

         // Falsies.
         Assert.False(parser.ParseExpression("false")(c));
         Assert.False(parser.ParseExpression("null")(c));
         Assert.False(parser.ParseExpression("0")(c));
         Assert.False(parser.ParseExpression("0.0")(c));

         // More advanced.
         Assert.True(parser.ParseExpression(" ! ''  ")(c));
         Assert.False(parser.ParseExpression(" ! '   '  ")(c)); // "   " -> true
         Assert.False(parser.ParseExpression(" ! !  0 ")(c));
         Assert.True(parser.ParseExpression("  'ab' || false  ")(c));
         Assert.False(parser.ParseExpression("'ab' && false")(c));
         Assert.True(parser.ParseExpression("'ab' && 'false' ")(c));

         Assert.False(parser.ParseExpression("NullString")(c));
         Assert.False(parser.ParseExpression("NullObject")(c));
         Assert.True(parser.ParseExpression("StrAsObj")(c)); // object = true
      }

      private Boolean _ParseAndRun(String expr)
      {
         var p = new PredicateParser<Context>();
         return p.ParseExpression(expr)(new Context());
      }

      [Fact]
      public void Associativity_tests()
      {
         // We can test associativity by lifting off things like int -> double conversions.

         // Left to right associativity.
         Assert.True(_ParseAndRun("11 / 2 / 2.0 == (11 / 2) / 2.0"));
         Assert.True(_ParseAndRun("11 / (2 / 2.0) == 11.0"));
      }

      // Tests features not currently supported.
      [Fact]
      public void Unsupported_features()
      {
         // Unsupported feature: We don't support static method calls.
         // A static method call on our context is allowed, if using the direct syntax.
         _ParseAndRun("StaticBool()");
         try
         {
            _ParseAndRun(typeof(Context).Namespace + ".Context.StaticBool()");
            Assert.True(false);
         }
         catch (SyntaxException) { /* OK */ }

         // Unsupported feature: an object that is a string is not cast as a string when converted to boolean.
         Assert.False(_ParseAndRun("''")); // explicit empty string
         Assert.True(_ParseAndRun("StrAsObj")); // empty string stored as Object, empty but non-null
         Assert.False(_ParseAndRun("StrAsObj.ToString()"));

         // Unsupported feature: a nested class cannot be accessed.
         // Nested class use the syntax Foo+Bar, and we don't support resolving these.
         // If we ever desire to support this we should use an alternative syntax because this is probably impossible to implement in our language.
         var bazzooba = typeof(Foo.BarNested).FullName;
         try
         {
            _ParseAndRun(bazzooba);
            Assert.True(false);
         }
         catch (SyntaxException) { /* OK */ }

         // Unsupported feature: indexers with multiple arguments. Not hard to do, just not supported at the moment.
         try
         {
            _ParseAndRun("this[1,2]");
            Assert.True(false);
         }
         catch (SyntaxException) { /* OK */ }

         // Unsupported feature: parameter values are not coerced when resolving a method call.
         // This is harder than it may look and not supported for now.
         try
         {
            _ParseAndRun("FuncTakingBool('this string is not coerced to true')");
         }
         catch (SyntaxException) { /* OK */ }
      }

      [Fact]
      public void String_escaping_supported_through_backtick()
      {
         var p = new ExpressionParser<Context, String>();
         Func<String, String> c = s => p.ParseExpression(s)(new Context());

         Assert.False(_ParseAndRun(@"'\m' == 'm'")); // we don't use \.
         Assert.Equal(@"\n", c(@"'\n'"));
         Assert.Equal("x \n x", c("'x `n x'"));
         Assert.Equal("x \n", c("'x `n'"));
         Assert.Equal("\n x", c("'`n x'"));
         Assert.Equal(@"\", c(@"'`\'"));

         // Unknown escape sequence:
         Assert.Equal("z", c("'`z'"));
      }

      [Fact]
      public void Issue_with_method_calls()
      {
         // HIGHESTGENERATION(SOURCEDATAROOT + '\main')
         Assert.True(_ParseAndRun("EchoOne(Foobar + 'blah') == 'foobarblah'"));
      }

      [Fact]
      public void Parser_handles_boxing()
      {
         Assert.True(_ParseAndRun("BoxedTrue"));
         Assert.False(_ParseAndRun("BoxedFalse"));

         Assert.True(_ParseAndRun("BoxedInt == 7"));
         Assert.True(_ParseAndRun("BoxedInt == true")); // non zero int
         Assert.True(_ParseAndRun("BoxedZeroInt == false"));

         Assert.True(_ParseAndRun("BoxedInt == BoxedTrue"));
         Assert.True(_ParseAndRun("BoxedInt"));
         Assert.False(_ParseAndRun("BoxedZeroInt"));
      }

      //    [Fact]
      // todo: nice feature?
      public void Can_convert_strings_to_numbers()
      {
         Assert.True(_ParseAndRun("+'2' = 2"));

         var p = new ExpressionParser<Context, Int32>();
         Assert.Equal(2, p.ParseExpression("+'2'")(new Context()));
      }

      [Fact]
      public void Some_number_conversion_tests()
      {
         var parser = new ExpressionParser<Context, Double>();

         Assert.Equal(2.0, parser.ParseExpression("1 + 1.0")(new Context()));

         var p2 = new ExpressionParser<Context, Object>();
         Assert.True(p2.ParseExpression("1.0")(new Context()) is Double);

         var p3 = new ExpressionParser<Context, Double>();
         Assert.Equal(1.0, p3.ParseExpression("1")(new Context()));
      }

      [Fact]
      public void Culture_specific_tests()
      {
         var currentCulture = Thread.CurrentThread.CurrentCulture;
         try
         {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("nl-NL");

            var parser = new ExpressionParser<Context, String>();
            var c = new Context();
            var flags = ExpressionParsingFlags.UseCurrentCulture;

            var result = parser.ParseExpression("2.1.ToString()")(c);
            Assert.Equal("2.1", result); // no flag was specified

            result = parser.ParseExpression("2.1.ToString()", null, flags)(c);
            Assert.Equal("2,1", result); // this time flags were specified
         }
         finally
         {
            Thread.CurrentThread.CurrentCulture = currentCulture;
         }
      }

      [Fact]
      public void Case_insensitivity_tests()
      {
         var parser = new ExpressionParser<Context, Boolean>();
         var c = new Context();
         var flags = ExpressionParsingFlags.IgnoreCase;

         // Ignorecase ensures identifiers are also resolved irregardless of case.
         Assert.True(parser.ParseExpression("Foobar == foOBar", null, flags)(new Context()));

         // Regex matching is case insensitive now.
         Assert.True(parser.ParseExpression("'foObAR' =~ /foobar/", null, flags)(new Context()));
         // But we can override this behaviour:
         Assert.False(parser.ParseExpression("'foObAR' =~ /foobar/I", null, flags)(new Context()));

         // String comparison.
         Assert.True(parser.ParseExpression("'FOOBAR' = 'foobar'", null, flags)(new Context()));
      }

      [Fact]
      public void General_expression_tests()
      {
         PredicateParser<Context> parser = new PredicateParser<Context>();

         // Ambiguous, this could be interpreted as:
         // 1. 'false' as truthy, so true
         // 2. String conversion goes first (current behaviour).
         Assert.False(parser.ParseExpression("'False' == true")(new Context()));
         Assert.True(parser.ParseExpression("1 == true")(new Context()));
         Assert.True(parser.ParseExpression("true=1")(new Context()));

         Assert.True(parser.ParseExpression("1 < ∞")(new Context()));

         Assert.True(parser.ParseExpression("1 > -∞")(new Context()));
         Assert.True(parser.ParseExpression("-∞ < ∞")(new Context()));
         Assert.True(parser.ParseExpression("~0xF0F0F0F0 = 0x0F0F0F0F")(new Context()));
         Assert.True(parser.ParseExpression("~~0xF0F0F0F0 = 0xF0F0F0F0")(new Context()));
         Assert.True(parser.ParseExpression("~~~0xF0F0F0F0 = 0x0F0F0F0F")(new Context()));
         Assert.True(parser.ParseExpression("0 = ~-1")(new Context()));

         Assert.True(parser.ParseExpression("2 + -2 = 1 - +1")(new Context()));

         // Note how this implies the lack of a unary -- operator!
         Assert.True(parser.ParseExpression("--2 = 2")(new Context()));

         // This is ok for now, don't think we need to disallow it.
         Assert.True(parser.ParseExpression("+'foobar'")(new Context()));

         Assert.True(_ParseAndRun("π ≤ ∞"));
         Assert.True(_ParseAndRun("π ≥ -∞"));

         // Double parse tests.
         Assert.Equal(true, parser.ParseExpression("5e+2 == 50E1")(new Context()));
         Assert.Equal(true, parser.ParseExpression("0.2 == .2")(new Context()));
         Assert.Equal(true, parser.ParseExpression(".2e-2 == 0.200E-02")(new Context()));
         Assert.Equal(true, parser.ParseExpression("-.2E100 > -.2e+110")(new Context()));
         Assert.Equal(true, parser.ParseExpression("-2.99e+1 == -2.99e+1")(new Context()));
         Assert.Equal(true, parser.ParseExpression("-2.99e1 == -2.99e1")(new Context()));

         Assert.Equal(true, parser.ParseExpression(" Name2 == 'name2'")(new Context())); // allow diigts in names
         Assert.Equal(true, parser.ParseExpression("Ar.Length == 4")(new Context()));

         Assert.Equal(true, parser.ParseExpression("1.4 == (1 + 0.2 * 2.0)")(new Context()));
         Assert.Equal(true, parser.ParseExpression("1.4 == (1.0 + 0.2 * 2.0)")(new Context()));
         Assert.Equal(true, parser.ParseExpression("1.9 > (2 - 0.2 * (2))")(new Context()));

         Assert.Equal(true, parser.ParseExpression("and == and and true")(new Context()));

         Assert.Equal(true, parser.ParseExpression("NV['foo'] == 'bar'")(new Context()));
         Assert.Equal(true, parser.ParseExpression("NV['foo'] != null")(new Context()));

         Assert.Equal(true, parser.ParseExpression("'c_foo' != null")(new Context()));
         Assert.Equal(true, parser.ParseExpression("ImNull == null")(new Context()));
         Assert.Equal(true, parser.ParseExpression("'c_foo' == 'c_foo'")(new Context()));

         Assert.Equal(true, parser.ParseExpression("Foo['anything'] == 'foo' ")(new Context()));

         Assert.Equal(true, parser.ParseExpression("(Ar)[0] == 10")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Ar[0] == 10")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Ar[0] != 20")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Ar[One] == 20")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Ar[Ar[3]] == 20")(new Context()));
         Assert.Equal(true, parser.ParseExpression("and == 'and' and 'and' == and")(new Context()));

         Assert.Equal(true, parser.ParseExpression("4 < -1 + 10")(new Context()));
         Assert.Equal(true, parser.ParseExpression("2 * 2 == 2 * (1 + 1)")(new Context()));

         Assert.Equal(true, parser.ParseExpression("true || false")(new Context()));
         Assert.Equal(true, parser.ParseExpression("false || false || false || true")(new Context()));
         Assert.Equal(false, parser.ParseExpression("false || false")(new Context()));
         Assert.Equal(true, parser.ParseExpression("true && true && true")(new Context()));
         Assert.Equal(false, parser.ParseExpression("true && true && false")(new Context()));
         Assert.Equal(true, parser.ParseExpression("true")(new Context()));
         Assert.Equal(false, parser.ParseExpression("false")(new Context()));

         Assert.Equal(true, parser.ParseExpression("(1 < 3 && 2 > -1) == true")(new Context()));
         Assert.Equal(true, parser.ParseExpression("true == (1 < 3 && 2 > -1) == true")(new Context()));

         Assert.Equal(true, parser.ParseExpression("Foo.Bar != 'foo'")(new Context()));
         Assert.Equal(true, parser.ParseExpression("this.Foo.Bar == 'bar'")(new Context()));
         Assert.Equal(true, parser.ParseExpression("this.Foobar == 'foobar'")(new Context()));

         Assert.Equal(true, parser.ParseExpression("1<2==true")(new Context()));
         Assert.Equal(false, parser.ParseExpression("1==2==true")(new Context()));
         Assert.Equal(true, parser.ParseExpression("1==1==true")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Thrifty > -10.0 || Dbl != 2.0")(new Context()));

         Assert.Equal(true, parser.ParseExpression("Thrifty == 30  ")(new Context()));
         Assert.Equal(true, parser.ParseExpression(" Thrifty != 42")(new Context()));
         Assert.Equal(true, parser.ParseExpression(" Thrifty > 0")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Thrifty >= 30")(new Context()));
         Assert.Equal(true, parser.ParseExpression("  Thrifty <= 30  ")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Thrifty < 50  ")(new Context()));

         Assert.Equal(true, parser.ParseExpression("Dbl == 2.0  ")(new Context()));
         Assert.Equal(true, parser.ParseExpression(" Dbl != 4.0")(new Context()));
         Assert.Equal(true, parser.ParseExpression(" Dbl > 0")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Dbl >= 2.000")(new Context()));
         Assert.Equal(true, parser.ParseExpression("  Dbl <= 30.1  ")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Dbl < 50  ")(new Context()));

         Assert.Equal(true, parser.ParseExpression("Foobar == 'foobar'")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Foobar != 'barfoo'")(new Context()));

         Assert.Equal(false, parser.ParseExpression("Thrifty != 30.0")(new Context())); // note: we allow the conversion here, if implicit
         Assert.Equal(true, parser.ParseExpression("Thrifty == 30.0")(new Context()));
         Assert.Equal(true, parser.ParseExpression("Thrifty > 20.0")(new Context()));

         Assert.Equal(true, parser.ParseExpression("this.Obj == Obj")(new Context()));
      }

      private static void AssertError(Action a)
      {
         try
         {
            a();
            Assert.True(false, "Expected exception. Instead succeeded.");
         }
         catch (Xunit.Sdk.AssertException)
         {
            throw;
         }
         catch (SyntaxException e)
         {
            if (e.InnerException is IndexOutOfRangeException) // not ok
               throw;
            // OK
         }
      }

      [Fact]
      public void Bad_syntax_is_caught()
      {
         PredicateParser<Context> parser = new PredicateParser<Context>();

         AssertError(() => parser.ParseExpression("2Foo.2Bar == true", (o, n, ns, @default) =>
         {
            if (Char.IsNumber(n[0]))
               Assert.True(false, "Name cannot start with number.");

            return Expression.Constant(true);
         })(new Context()));

         AssertError(() => parser.ParseExpression("2.e+e+2 == 4.0")(new Context()));

         AssertError(() => parser.ParseExpression("'foobar")(new Context()));
         AssertError(() => parser.ParseExpression("true <=")(new Context()));
         AssertError(() => parser.ParseExpression("true an")(new Context()));
         AssertError(() => parser.ParseExpression("true and")(new Context()));
         AssertError(() => parser.ParseExpression("2bar == '2bar'")(new Context()));

         AssertError(() => parser.ParseExpression("")(new Context()));
         AssertError(() => parser.ParseExpression("2E")(new Context()));
         AssertError(() => parser.ParseExpression("2.3E+")(new Context()));
         AssertError(() => parser.ParseExpression("1 == 1 ==")(new Context()));
         AssertError(() => parser.ParseExpression("1 <<< 1")(new Context()));
         AssertError(() => parser.ParseExpression("1 ==")(new Context()));



         AssertError(() => parser.ParseExpression("Ar[0  ")(new Context()));
         AssertError(() => parser.ParseExpression("(Ar[0]")(new Context()));

         AssertError(() => parser.ParseExpression("'foobar'' == 'foobar'")(new Context()));
         //   AssertError(() => parser.ParseExpression("\"foobar\"")(new Context()));

         AssertError(() => parser.ParseExpression("Foo.2Bar == true")(new Context()));

         TestUtils.AssertThrowsContractException(() => parser.ParseExpression(null)(new Context()));
      }

      [Fact]
      public void Dynamic_name_resolution_works()
      {
         var p = new ExpressionParser<Context, Int32>();

         var c = p.ParseExpression("foo - 1+0.0", (e, n, tn, d) =>
         {
            if (tn == null)
               return Expression.Call(null, GetType().GetMethod("ResolveName", BindingFlags.Static | BindingFlags.Public, null, CallingConventions.Any, new[] { typeof(String) }, null),
                                      Expression.Constant(n));
            else return d(e, n, tn, d);
         });

         Assert.Equal(-2, c(new Context()));
      }

      public static Int32 ResolveName(String name)
      {
         if (name == "foo") return -1;
         if (name == "bar") return 5;
         throw new Exception();
      }
   }
}
