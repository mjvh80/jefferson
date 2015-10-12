using Jefferson.Binders;
using Jefferson.Directives;
using Jefferson.Output;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using Xunit;
using Xunit.Extensions;

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

   public enum EnumTest
   {
      Foo = 1,
      Bar = 2
   }

   public class TestContext : IndexerVariableBinder
   {
      private Dictionary<String, Func<Object>> _mVariables;
      private Dictionary<String, Type> _mTypes; // = new Dictionary<String, Type>();

      public TestContext(Boolean caseSensitive = false)
         : base(new Dictionary<String, Type>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase))
      {
         _mVariables = new Dictionary<String, Func<Object>>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
         _mTypes = (Dictionary<String, Type>)base.mTypeDeclarations;
      }

      public String GetRecursive()
      {
         return "$$foobar$$";
      }

      public String Trim(String s) { return s == null ? "" : s.Trim(); }

      public String GetLoop()
      {
         return "$$GetLoop()$$";
      }

      public String FieldOnCtx = "fldOnCtx";
      public String ReadWriteProp { get; set; }
      public String ReadProp { get; private set; }
      public String WriteProp { internal get; set; }

      public IEnumerable<Foobar> Foobars;

      public String HelloWorld(String world) { return "Hello " + world; }

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

      public Object this[String name]
      {
         get { return _mVariables[name](); }
         set { _mVariables[name] = () => value; }
      }

      public Type GetType(String variable)
      {
         Type result;
         return _mTypes.TryGetValue(variable, out result) ? result : null;
      }

      public override Expression BindVariableWrite(Expression currentContext, String name, Expression value)
      {
         var type = this.GetType();
         if (type.GetProperty(name) != null || type.GetField(name) != null) return null;
         return base.BindVariableWrite(currentContext, name, value);
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
      public void Can_use_cultures()
      {
         Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("NL-nl");

         var result = new TemplateParser(new TemplateOptions { UseCurrentCulture = true }).Replace("$$1.2$$", context);

         Assert.Equal("1,2", result.Trim());
      }

      [Fact]
      public void Can_refer_to_enums()
      {
         var parser = new ExpressionParser<TestContext, EnumTest>();
         var result = parser.ParseExpression("Jefferson.Tests.EnumTest.Foo");

         Assert.Equal(1, (Int32)result(context));
      }

      [Fact]
      public void Deep_replacing_works()
      {
         var p = new TemplateParser(new IfDirective(), new LetDirective());
         context.Add("include", "' $$#if b1$$ BLAH BLAH $$#else$$ FOOD $$/if$$ '");
         var result = (p.ReplaceDeep(@"Blah blah blah
         
$$#if b1$$
$$#let foo = '$`$b1$`$'$$
   if if if: $$foo$$ 
$$include$$
$$/let$$
$$#else$$
   else else else
$$/if$$
x
         ", context));

         Assert.Equal(
@"
Blah blah blah
         


   if if if: True 
'  BLAH BLAH  '


x".Trim(), result.Trim());
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
$$#if true$$
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
      public void Can_evaluate_expressions_in_template_context()
      {
         var p = new TemplateParser();
         var v = p.EvaluateExpression("b1", context);

         var b = v as Boolean?;
         Assert.NotNull(b);
         Assert.Equal(true, b.Value);
      }

      [Fact]
      public void Can_handle_bad_expressions()
      {
         try
         {
            replacer.Replace("$$FOOOOBAAAARRR$$", context);
            Assert.True(false, "expected error due to unresolved name");
         }
         catch (SyntaxException e)
         {
            Assert.Equal(
  @"Expected known name, could not resolve 'FOOOOBAAAARRR'

1: FOOOOBAAAARRR
                ^ (14)", e.Message);
         }
      }

      [Theory]
      [InlineData("$$ ?FOOBAR$$", "Expected identifier")]
      public void Can_handle_bad_syntax(String badInput, String expectedErrorPart)
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser().Replace(badInput, context));
         Assert.Contains(expectedErrorPart, error.Message);
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

         Assert.Equal("Hello foobar world!", repl2.Replace("Hello $$Foobar$$ world!", customCtx));

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

      [Fact]
      public void By_default_undefined_names_give_an_error()
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser().Replace("$$ IDONTEXIST $$", context));
         Assert.Contains("Expected known name", error.Message);
      }

      [Fact]
      public void Can_allow_undefined_names()
      {
         var options = new TemplateOptions { AllowUnknownNames = true };
         var result = new TemplateParser(options).Replace("Hi $$SOMEONE$$", context);
         Assert.Equal("Hi", result.Trim());
      }

      [Fact]
      public void Can_allow_undefined_names_in_expressions()
      {
         var result = new TemplateParser().Replace("Hi $$? SOMEONE $$", context);
         Assert.Equal("Hi", result.Trim());
      }

      [Fact]
      public void Can_allow_undefined_names_in_expressions_even_when_this_is_the_default()
      {
         var options = new TemplateOptions { AllowUnknownNames = true };
         var result = new TemplateParser(options).Replace("Hi $$? SOMEONE $$", context);
         Assert.Equal("Hi", result.Trim());
      }

      [Fact]
      public void Can_use_undefined_variables_as_value_types()
      {
         var result = new TemplateParser().Replace("Hi $$? SOMEONE + 7 $$", context);
         Assert.Equal("Hi 7", result.Trim());
      }

      [Fact]
      public void Can_make_templates_readonly()
      {
         var parser = new TemplateParser(new DefineDirective(), new UndefDirective());
         parser.Compile<TestContext>("FieldOnCtx", null, new ReadOnlyBinder())(new TestContext(), new StringBuilderOutputWriter());
         Assert.Throws<SyntaxException>(() => parser.Compile<TestContext>("$$#define FieldOnCtx = 'foobar' /$$", null, new ReadOnlyBinder())(new TestContext(), new StringBuilderOutputWriter()));
         Assert.Throws<SyntaxException>(() => parser.Compile<TestContext>("$$#undef FieldOnCtx /$$", null, new ReadOnlyBinder())(new TestContext(), new StringBuilderOutputWriter()));

         // Can read though.
         var writer = new StringBuilderOutputWriter();
         parser.Compile<TestContext>("$$ FieldOnCtx $$", null, new ReadOnlyBinder())(new TestContext(), writer);
         Assert.Equal("fldOnCtx", writer.ToString().Trim());
      }

      [Fact]
      public void Allow_empty_expressions()
      {
         var r = replacer.Replace("foobar: $$$$ $$ $$ $$//$$ $$ //$$ $$// $$ $$ // $$", context);
         Assert.Equal("foobar:", r.Trim());
      }

      [Fact]
      public void Empty_expression_has_type_string()
      {
         var result = new TemplateParser(new LetDirective()).Replace("$$#let a=$$ $$a.GetType().Name$$ $$/let$$", context);
         Assert.Equal("String", result.Trim());
      }

      [Fact]
      public void Debug_viewer_works()
      {
         Func<String, Int32> f = s => Int32.Parse(s);
         var dbgCtx = new ExpressionParser<String, Int32>._ExpressionDebugContext("foo!", f);
         Assert.Equal("foo!", dbgCtx.ToString());
      }

      public class TestDirective : IDirective
      {
         public String Name { get; set; }
         public String[] ReservedWords { get; set; }
         public Boolean MayBeEmpty { get; set; }
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
      public void Can_write_to_a_generic_text_writer()
      {
         var buffer = new StringBuilder();
         using (var writer = new StringWriter(buffer))
         {
            var p = new TemplateParser();
            p.Compile<TestContext>("just write text", null, null)(context, new TextWriterOutputWriter(writer));
         }
         Assert.Equal("just write text", buffer.ToString());
      }

      [Fact]
      public void Can_create_template_parser_without_any_directives()
      {
         var p = new TemplateParser(new TemplateOptions(), null); // causes none to be defined (incl. no defaults)
         Assert.Equal("foobar", p.Replace("foobar", context));

         // Try to use #if, it shouldn't work.
         var source = "foobar $$#if b1$$ blah $$/if$$";
         Assert.Throws<SyntaxException>(() => p.Replace(source, context));

         p = new TemplateParser(new IfDirective());
         p.Replace(source, context); // works this time
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
            p.Replace(source, context);
         }
         catch (Exception e)
         {
            Assert.Equal("throw worked", e.Message);
         }
      }

      [Fact]
      public void Custom_value_filters_work()
      {
         var values = new StringBuilder();

         var p = new TemplateParser();
         p.ValueFilter = (s, o) =>
         {
            values.AppendFormat("Var '{0}' resolved to '{1}'.", s, o).AppendLine();
            return o;
         };

         var ignoredOutput = p.Replace("$$ b1 $$ and blah $$ FieldOnCtx $$.", context);

         // Note: that FieldOnCtx name is *not* part of the output here.
         Assert.Equal(
@"Var 'b1' resolved to 'True'.
Var 'FieldOnCtx' resolved to 'fldOnCtx'.
", values.ToString());

         // Trace.WriteLine(values.ToString());
      }

      [Fact]
      public void Can_compare_enum_value_to_string()
      {
         // Jefferson.Tests.EnumTest;

         var result = replacer.Replace(@"
 
             $$#define foo = Jefferson.Tests.EnumTest.Foo/$$

             $$ foo = 'Foo' $$

         ", context).Trim();

         Assert.Equal("True", result);


         result = replacer.Replace(@"
 
             $$#define foo = Jefferson.Tests.EnumTest.Foo/$$

             $$ 'Bar' = foo $$

         ", context).Trim();

         Assert.Equal("False", result);
      }

      [Fact]
      public void Invalid_enums_are_detected()
      {
         // Jefferson.Tests.EnumTest;

         Assert.Throws<ArgumentException>(() => replacer.Replace(@"
 
             $$#define foo = Jefferson.Tests.EnumTest.Foo/$$

             $$ foo = 'f00' $$

         ", context));
      }

      [Fact]
      public void Can_set_fields_and_props_on_context()
      {
         replacer.Replace("$$#define FieldOnCtx='foobar' /$$", context);
         Assert.Equal("foobar", context.FieldOnCtx);

         replacer.Replace("$$#define ReadWriteProp='foobar' /$$", context);
         Assert.Equal("foobar", context.ReadWriteProp);

         replacer.Replace("$$#define WriteProp = 'bar' /$$", context);
         Assert.Equal("bar", context.WriteProp);
      }

      [Fact]
      public void Can_enable_tracing()
      {
         var stringWriter = new StringWriter();
         var traceListener = new System.Diagnostics.TextWriterTraceListener(stringWriter);
         Trace.Listeners.Add(traceListener);

         var options = new TemplateOptions();
         options.EnableTracing = true;

         var p = new TemplateParser(options);

         p.Replace("$$#if true$$ foobar $$/if$$", context);

         traceListener.Flush();

         Assert.True(stringWriter.ToString().Length > 0);
      }

      [Fact]
      public void Tracing_is_disabled_by_default()
      {
         var stringWriter = new StringWriter();
         var traceListener = new System.Diagnostics.TextWriterTraceListener(stringWriter);
         Trace.Listeners.Add(traceListener);

         var options = new TemplateOptions();
         var p = new TemplateParser(options);

         p.Replace("$$#if true$$ foobar $$/if$$", context);

         traceListener.Flush();

         Assert.Equal(0, stringWriter.ToString().Trim().Length);
      }
   }
}
