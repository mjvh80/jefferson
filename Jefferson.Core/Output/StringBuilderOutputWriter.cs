using System;
using System.Text;

namespace Jefferson.Output
{
   public class StringBuilderOutputWriter : IOutputWriter
   {
      private readonly StringBuilder _mStrBldr;

      internal StringBuilderOutputWriter() : this(new StringBuilder()) { }

      public StringBuilderOutputWriter(StringBuilder target)
      {
         Ensure.NotNull(target, "target");
         _mStrBldr = target;
      }

      public void Write(String chunk)
      {
         _mStrBldr.Append(chunk);
      }

      internal String GetOutput()
      {
         return _mStrBldr.ToString();
      }

      public override string ToString()
      {
         return GetOutput();
      }
   }
}
