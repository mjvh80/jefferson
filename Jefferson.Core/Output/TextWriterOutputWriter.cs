using System;
using System.IO;

namespace Jefferson.Output
{
   public class TextWriterOutputWriter : IOutputWriter
   {
      private readonly TextWriter _mTarget;

      public TextWriterOutputWriter(TextWriter writer)
      {
         Ensure.NotNull(writer, "writer");
         _mTarget = writer;
      }

      public void Write(String chunk)
      {
         _mTarget.Write(chunk);
      }
   }
}
