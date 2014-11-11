using Jefferson.Directives;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;

namespace Jefferson.Tests
{
   #region Test Context Classes

   public class Foobar
   {
      public String Bazzy;

      public String[] Nested = new[] { "nested1", "nested2" };

      public override String ToString()
      {
         return "Foobar: " + Bazzy;
      }
   }

   public class TestContext : IVariableDeclaration
   {
      public TestContext() { }

      public String GetRecursive()
      {
         return "$$foobar$$";
      }

      public String GetLoop()
      {
         return "$$GetLoop()$$";
      }

      public IEnumerable<Foobar> Foobars;

      public String[] EmptyStrAr = new String[0];

      // Declare the variable.
      public void Add(String variable, String value, Boolean isExpr = false)
      {
         variable = variable.Trim('$');

         Type type = null;

         if (isExpr)
         {
            var p = new ExpressionParser<TestContext, Object>();
            var r = p.ParseExpression(value);

            // First run determines variable type here.
            _mVariables[variable] = () => r(this);
            type = r(this).GetType();
         }
         else
         {
            Func<Object> getValue = () =>
            {
               var t = new TemplateParser();
               var r = t.Replace(value, this); // t.Compile<TestContext>(value, typeof(TestContext), this, false);
               return r;
            };
            _mVariables[variable] = getValue;
            type = typeof(String);
         }

         if (_mTypes.ContainsKey(variable))
         {
            if (_mTypes[variable] != type) throw new Exception("todo");
         }
         else // declare the type
            _mTypes.Add(variable, type);
      }

      private Dictionary<String, Func<Object>> _mVariables = new Dictionary<String, Func<Object>>();
      private Dictionary<String, Type> _mTypes = new Dictionary<String, Type>();
      public Object this[String name] { get { return _mVariables[name](); } }

      public Type GetType(String variable)
      {
         Type result;
         return _mTypes.TryGetValue(variable, out result) ? result : null;
      }
   }

   #endregion

   public class TemplateParserTests
   {
      TemplateParser replacer;
      TestContext context;

      public TemplateParserTests()
      {
         var ctx = context = new TestContext();
         replacer = new TemplateParser();

         ctx.Add("$$def$$", "$$abc$$ en bah"); // note: abc is not defined
         ctx.Add("$$abc$$", "boe");

         ctx.Add("$$b1$$", "true", true);
         ctx.Add("$$b2$$", "false", true);

         ctx.Add("$$c1$$", "true", false);
         ctx.Add("$$c2$$", "false", false);

         ctx.Add("recursive", "GetRecursive()", true);
         ctx.Add("foobar", "qux");
      }

      [Fact]
      public void Test_simple_expressions()
      {
         Assert.Equal("A False B boe", replacer.Replace("A $$(b1 && b2)$$ B $$abc$$", context));
         Assert.Equal("A boe B", replacer.Replace("A $$abc$$ B", context));
         Assert.Equal("A boe en bah B boe", replacer.Replace("A $$def$$ B $$abc$$", context));
         Assert.Equal("A True B boe", replacer.Replace("A $$(b1 || b2)$$ B $$abc$$", context));
         Assert.Equal("A False B boe", replacer.Replace("A $$(b1 && b2)$$ B $$abc$$", context));
         Assert.Equal("A False B boe", replacer.Replace("A $$(!b1)$$ B $$abc$$", context));
         Assert.Equal("True", replacer.Replace("$$(b1)$$", context));
         Assert.Equal("True", replacer.Replace("$$(b1)$$", context));
         Assert.Equal("False", replacer.Replace("$$(!b1)$$", context));

         // True here because c1 and c2 are treated as strings which implicitly convert to true.
         Assert.Equal("A True B boe", replacer.Replace("A $$(c1 && c2)$$ B $$abc$$", context));

         // If the result of a method call is another variable expression we cannot at compile time handle this.
         Assert.Equal("A $$foobar$$ B", replacer.Replace("A $$recursive$$ B", context));
         Assert.Equal("A qux B", replacer.ReplaceDeep("A $$recursive$$ B", context));
      }

      [Fact]
      public void If_directives_work()
      {
         Assert.Equal("Foo is the new bar!", replacer.Replace("Foo is the new $$#if b1$$bar$$/if$$!", context));
         Assert.Equal("Foo is the new !", replacer.Replace("Foo is the new $$#if !b1$$bar$$/if$$!", context));

         // Test nested if:
         Assert.Equal("Foo is the new baa qux aarrr!", replacer.Replace("Foo is the new $$#if b1$$b$$#if !b2$$aa $$foobar$$ aa$$/if$$rrr$$/if$$!", context));

         // If/Else tests
         Assert.Equal("Foo is the new ELSE !", replacer.Replace("Foo is the new$$#if !b1$$ IF $$#else$$ ELSE $$/if$$!", context));
         Assert.Equal("Foo is the new ELIF !", replacer.Replace("Foo is the new$$#if !b1$$ IF $$#elif (b1)$$ ELIF $$#else$$ ELSE $$/if$$!", context));
         Assert.Equal("Foo is the new  NESTED  ELIF !", replacer.Replace("Foo is the new$$#if !b1$$ IF $$#elif b1$$ $$#if b1$$ NESTED $$/if$$ ELIF $$#else$$ ELSE $$/if$$!", context));
         Assert.Equal("Foo is the new  NESTED2  ELIF !", replacer.Replace("Foo is the new$$#if !b1$$ IF $$#elif b1$$ $$#if !b1$$ HAHA $$#else$$ NESTED2 $$/if$$ ELIF $$#else$$ ELSE $$/if$$!", context));
      }

      [Fact]
      public void Errors_are_informative()
      {
         //  replacer.Replace("If no end: $$#if$$ foobar $$/if$$ blah.", context);

         Func<Action, String> getError = a =>
            {
               try { a(); }
               catch (Exception e) { return e.Message; }
               return null;
            };

         var error = getError(() => replacer.Replace(
@"Foo is $$#if
     the new bar!
", context));

         Assert.Equal(
@"Could not find end of directive.

1: Foo is $$#if
          ^ (8)", error);

         error = getError(() => replacer.Replace("If no end: $$#if foobar $$/if$$ blah.", context));
         Assert.Equal(
@"Could not find end of directive.

1: If no end: $$#if foobar $$/if$$ blah.
              ^ (12)", error);

         error = getError(() => replacer.Replace("If no end: $$#if $$ foobar $$if$$ blah.", context));
         Assert.Equal(
@"Failed to find directive end '$$/if$$' for directive 'if'.

1: If no end: $$#if $$ foobar $$if$$ blah.
              ^ (12)", error);

         error = getError(() => replacer.Replace("If empty expression: $$#if$$ foobar $$/if$$ blah.", context));
         Assert.Equal(
@"Empty expression in if or elif.

1: If empty expression: $$#if$$ foobar $$/if$$ blah.
                               ^ (29)", error);
      }

      [Fact]
      public void Error_line_numbers_in_nested_directives_are_accurate()
      {
         Func<Action, String> getError = a =>
            {
               try { a(); }
               catch (Exception e) { return e.Message; }
               return null;
            };

         var error = getError(() => replacer.Replace(
@"Line 1
Line 2
$$#if true#$$
Line 4
  $$#if false$$
    Line 6
  $$#elif 
    Line 8
  $$/if$$
$$/if$$", context));

         Assert.Equal(
@"Could not find matching $$.

7:   $$#elif 
     ^ (3)", error);
      }

      [Fact]
      public void Eeach_directive_works()
      {
         context.Foobars = new List<Foobar>
         {
            new Foobar { Bazzy = "foo1" },
            new Foobar { Bazzy = "foo2", Nested = new[] { "NF2_0", "NF2_1" } },
            new Foobar { Bazzy = "foo3" }
         };

         var result = (replacer.Replace(
@"
Foo:
$$#each Foobars$$
- got $$Bazzy$$, parent: $$ $1.foobar $$
$$/each$$
done
", context));

         // Trace.WriteLine(result);

         // Note the whitespace here, which is caused by the absence of the directives.
         // It seems handlebars uses {{~#if ... ~}} or jinja2 uses {{-...-}}, could add this too.
         Assert.Equal(
@"
Foo:

- got foo1, parent: qux

- got foo2, parent: qux

- got foo3, parent: qux

done
", result);

         // Each else works with an empty enumerator.
         result = (replacer.Replace(
@"
Foo:
$$#each EmptyStrAr $$
- got $$Bazzy$$, parent: $$ $1.foobar $$
$$#else$$
WOOHOO here $$foobar$$
$$/each$$
done
", context));

         Assert.Equal(
@"
Foo:

WOOHOO here qux

done
", result);

         // And we can nest some stuff.
         result = (replacer.Replace(
@"
Foo:
$$#each Foobars $$
   $$#if Bazzy =~ /foo1/ $$
      Got foo1 in if.
   $$#else$$
      Got something else: $$Bazzy$$.
   $$/if$$
$$#else$$
WOOHOO here $$foobar$$
$$/each$$
done
", context));

         // Trace.WriteLine(result);

         Assert.Equal(
@"
Foo:

   
      Got foo1 in if.
   

   
      Got something else: foo2.
   

   
      Got something else: foo3.
   

done
", result);

         // Can nest each directives.
         // todo: this.GetType().FullName fails
         result = replacer.Replace(@"
Foo:
$$#each Foobars$$
 Nested: ($$#each Nested$$
    $$ this $$
 $$/each$$)
$$/each$$

", context);

         // Trace.WriteLine(result);
      }

      [Fact]
      public void Loops_are_detected()
      {
         try
         {
            replacer.ReplaceDeep("$$GetLoop()$$", context);
         }
         catch (Exception e)
         {
            /* Assume loop detected. */
            Assert.Equal("Possible loop detected in ReplaceDeep, stopping after 1000 iterations.", e.Message);
            return;
         }
         Assert.True(false);
      }

      [Fact]
      public void Can_handle_bad_expressions()
      {
         // Unknown names resolve to empty strings.
         Assert.Equal("", replacer.Replace("$$FOOOOBAAAARRR$$", context));

         // Now expect an exception.
         try
         {
            Assert.Equal("", replacer.Replace("$$FOOOOBAAAARRR$$", context, except: true));
            Assert.True(false, "expected exception due to undefined name");
         }
         catch
         {
            // OK
         }
      }

      public class CustomVarContext : TestContext
      {
         public String Foobar = "foobar";
      }

      [Fact]
      public void CustomVarContext_works()
      {
         var repl2 = new TemplateParser();
         var customCtx = new CustomVarContext();

         Assert.Equal("Hello foobar world!", repl2.Replace("Hello $$foobar$$ world!", customCtx, except: true));

         // todo: more testing
      }

      [Fact]
      public void Cannot_register_same_directive_twice()
      {
         try
         {
            var parser = new TemplateParser(new IfDirective(), new IfDirective());
            Assert.True(false);
         }
         catch
         {
            // OK
         }
      }

      public class TestDirective : IDirective
      {
         public String Name { get; set; }
         public String[] ReservedWords { get; set; }
         public Expression Result;
         public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
         {
            return Result;
         }
      }

      [Fact]
      public void Clashing_keywords_are_detected()
      {
         try
         {
            var parser = new TemplateParser(new IfDirective(), new TestDirective { Name = "bad", ReservedWords = new[] { "if" } });
            Assert.True(false);
         }
         catch
         {
            // OK
         }

         // Can actually register this if nothing's bad, however.
         var parser2 = new TemplateParser(new IfDirective(), new TestDirective { Name = "bad", ReservedWords = null });
      }

      [Fact]
      public void Directives_must_have_valid_names()
      {
         try
         {
            var p = new TemplateParser(new TestDirective { Name = null });
            Assert.True(false);
         }
         catch
         {
            // OK
         }

         try
         {
            var p = new TemplateParser(new TestDirective { Name = "i haz spacey" });
            Assert.True(false);
         }
         catch
         {
            // OK
         }
      }

      [Fact]
      public void Can_create_custom_directive()
      {
         var customDirective = new TestDirective
         {
            Name = "throw",
            ReservedWords = null,
            Result = Expression.Throw(Expression.New(Utils.GetConstructor(() => new Exception("")), Expression.Constant("throw worked")))
         };

         var source = "Hello $$#throw$$ $$/throw$$ world.";
         try
         {
            var p = new TemplateParser(customDirective);
            p.Replace(source, context, true);
         }
         catch (Exception e)
         {
            Assert.Equal("throw worked", e.Message);
         }
      }
   }
}
