﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jefferson.Tests
{
   class Test_Directive_Each
   {
      TemplateParser replacer;
      TestContext context;

      public Test_Directive_Each()
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
- got $$ this $$, parent: $$ $1.foobar $$
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
   }
}
