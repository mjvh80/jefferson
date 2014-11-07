using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Jefferson.Tests
{
   class SanityChecks
   {
      /// <summary>
      /// An exception type must be serializable in order to be able to cross AppDomain boundaries.
      /// </summary>
      [Fact]
      public void All_exception_types_are_serializable()
      {
         var didTest = false;

         // Force reference to jefferson.
         var e = Utils.Error("foobar");
         Assert.Equal("foobar", e.Message);

         // Force all referenced assemblies to load, otherwise our exception types may not have loaded.
         var curAsm = GetType().Assembly;
         foreach (var refAsm in curAsm.GetReferencedAssemblies())
            Assembly.Load(refAsm);

         foreach (var exceptionType in AppDomain.CurrentDomain.GetAssemblies().SelectMany(asm => asm.GetTypes())
                                               .Where(typ => typ.Namespace != null && typ.Namespace.StartsWith("Jefferson"))
                                               .Where(typ => typeof(Exception).IsAssignableFrom(typ)))
         {
            var serializableAttributes = exceptionType.GetCustomAttributes(typeof(SerializableAttribute), inherit: false);
            Assert.NotNull(serializableAttributes);
            Assert.True(serializableAttributes.Length > 0);
            didTest = true;
         }
         Assert.True(didTest); // there's at least one such class in this library
      }

      [Fact]
      public void Line_number_counting_works()
      {
         var text = "foobar";

         Assert.Equal(1, SyntaxException._FindLineNumber(text, 0));
         Assert.Equal(1, SyntaxException._FindLineNumber(text, Int32.MaxValue));

         text = "foobar\nblah";
         Assert.Equal(1, SyntaxException._FindLineNumber(text, 0));
         Assert.Equal(1, SyntaxException._FindLineNumber(text, 1));
         Assert.Equal(1, SyntaxException._FindLineNumber(text, text.IndexOf('\n')));
         Assert.Equal(2, SyntaxException._FindLineNumber(text, text.IndexOf('\n') + 1));
         Assert.Equal(2, SyntaxException._FindLineNumber(text, Int32.MaxValue));

         //       1    2    3 4  5  6  7     8
         text = "xxx\nyyy\rzz\n\ra\nb\rcc\r\ndd\r";
         Assert.Equal(1, SyntaxException._FindLineNumber(text, 1));
         Assert.Equal(1, SyntaxException._FindLineNumber(text, 3));
         Assert.Equal(2, SyntaxException._FindLineNumber(text, 5));
         Assert.Equal(2, SyntaxException._FindLineNumber(text, text.IndexOf('\r')));
         Assert.Equal(3, SyntaxException._FindLineNumber(text, text.IndexOf('z')));
         Assert.Equal(3, SyntaxException._FindLineNumber(text, text.IndexOf('\n', 5)));
         Assert.Equal(5, SyntaxException._FindLineNumber(text, text.IndexOf("a\n"))); // LFCR here, not CR LF

         Assert.Equal(8, SyntaxException._FindLineNumber(text, text.Length - 1));

         Assert.Equal("xxx", SyntaxException._GetLine(text, -1));
         Assert.Equal("xxx", SyntaxException._GetLine(text, 0));
         Assert.Equal("xxx", SyntaxException._GetLine(text, 1));
         Assert.Equal("yyy", SyntaxException._GetLine(text, 4));
         Assert.Equal("yyy", SyntaxException._GetLine(text, 7));
         Assert.Equal("zz", SyntaxException._GetLine(text, 8));
         Assert.Equal("cc", SyntaxException._GetLine(text, text.IndexOf("c\r\nd")));
         Assert.Equal("cc", SyntaxException._GetLine(text, text.IndexOf("c\r\nd") + 1));
         Assert.Equal("cc", SyntaxException._GetLine(text, text.IndexOf("c\r\nd") + 2));
         Assert.Equal("dd", SyntaxException._GetLine(text, text.IndexOf("c\r\nd") + 3));
         Assert.Equal("dd", SyntaxException._GetLine(text, text.Length - 2));
         Assert.Equal("dd", SyntaxException._GetLine(text, text.Length - 1));
         Assert.Equal("dd", SyntaxException._GetLine(text, text.Length));
         Assert.Equal("dd", SyntaxException._GetLine(text, Int32.MaxValue));

         Assert.Equal("foobar", SyntaxException._GetLine("foobar", 0));
         Assert.Equal("foobar", SyntaxException._GetLine("foobar\n", 0));
         Assert.Equal("foobar", SyntaxException._GetLine("foobar\r", 0));
         Assert.Equal("foobar", SyntaxException._GetLine("foobar\r\n", 0));
         Assert.Equal("foobar", SyntaxException._GetLine("foobar\n\r", 0));

         var relPos = 5;
         var lineNum = SyntaxException._FindLineNumber("foo\nbar", ref relPos);
         Assert.Equal(1, relPos);

         relPos = 1;
         lineNum = SyntaxException._FindLineNumber("foo\nbar", ref relPos);
         Assert.Equal(1, relPos);

         relPos = 3;
         lineNum = SyntaxException._FindLineNumber("foo\nbar", ref relPos);
         Assert.Equal(3, relPos);
      }
   }
}
