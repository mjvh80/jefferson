using System;
using System.Text;

namespace Jefferson.Output
{
   public class StringBuilderOutputWriter : IOutputWriter
   {
      private readonly StringBuilder _mStrBldr;

      public StringBuilderOutputWriter(StringBuilder target)
      {
         Ensure.NotNull(target);
         _mStrBldr = target;
      }

      public void Write(String chunk)
      {
         _mStrBldr.Append(chunk);
      }
   }
}
