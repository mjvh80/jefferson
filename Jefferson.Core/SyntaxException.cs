using Jefferson.Extensions;
using System;
using System.Diagnostics.Contracts;

namespace Jefferson
{
   /// <summary>
   /// Represents a syntactic error. Supports informative error messages including line number, column number and error indicator (caret).
   /// </summary>
   [Serializable]
   public class SyntaxException : Exception
   {
      private const String _kDefaultMessage = "Error parsing expression";

      public static SyntaxException Create(Exception inner, String source, String msg, params Object[] args)
      {
         if (msg == null)
            msg = _kDefaultMessage;
         else
            msg = String.Format(msg, args);

         return new SyntaxException(msg + "\r\n\r\n" + source, inner);
      }

      public static SyntaxException Create(String source, String msg = null, params Object[] args)
      {
         if (msg == null)
            msg = _kDefaultMessage;
         else
            msg = String.Format(msg, args);

         return new SyntaxException(msg + "\r\n\r\n" + source);
      }

      public static SyntaxException Create(String source, Int32 position, String msg = null, params Object[] args)
      {
         Contract.Requires(source != null);
         return Create(null, source, position, msg, args);
      }

      public static SyntaxException Create(Exception inner, String source, Int32 position, String msg = null, params Object[] args)
      {
         Contract.Requires(source != null);

         var line = _GetLine(source, position);
         var lineNum = _FindLineNumber(source, ref position);
         var positionalMessage = lineNum + ": " + line + "\r\n" + new String(' ', lineNum.ToStringInvariant().Length) + "  ";
         if (position > 0) positionalMessage += new String(' ', position);
         positionalMessage += "^";
         if (position >= 0) positionalMessage += " (" + (position + 1).ToStringInvariant() + ")";

         if (msg == null)
            msg = _kDefaultMessage;
         else
            msg = String.Format(msg, args);

         return new SyntaxException(msg + "\r\n\r\n" + positionalMessage, inner);
      }

      internal static String _GetLine(String source, Int32 position)
      {
         Contract.Requires(source != null);

         if (source.Length == 0) return source;
         if (position > source.Length - 1) position = source.Length - 1;
         if (position < 0) position = 0;

         var cr = source.IndexOf('\r', position);
         var lf = source.IndexOf('\n', position);

         var end = cr < 0 ? source.Length : cr;
         end = lf < 0 ? end : Math.Min(lf, end);

         cr = position == 0 ? -1 : source.LastIndexOf('\r', position - 1);
         lf = position == 0 ? -1 : source.LastIndexOf('\n', position - 1);

         if (source[position] == '\n' && cr == position - 1)
         {
            cr = source.LastIndexOf('\r', position - 2);
            end -= 1;
         }

         var start = cr < 0 ? 0 : cr + 1;
         start = lf < 0 ? start : Math.Max(start, lf + 1);

         return source.Substring(start, Math.Max(0, end - start));
      }

      internal static Int32 _FindLineNumber(String source, Int32 position)
      {
         return _FindLineNumber(source, ref position);
      }

      internal static Int32 _FindLineNumber(String source, ref Int32 position)
      {
         Int32 line, idx, pos = position, initialPos = position;
         for (line = 1, idx = 0; ; line++, position = initialPos - idx, idx++)
         {
            var cr = source.IndexOf('\r', idx);
            var lf = source.IndexOf('\n', idx);
            if (cr < 0 && lf < 0) return line;
            if (cr >= pos && lf >= pos) return line;
            if (lf < cr)
            {
               if (lf > 0) line++;
               if (cr >= pos) return line;
               idx = cr + 1;
            }
            else if (lf > cr + 1)
            {
               if (cr > 0) line++;
               if (lf >= pos) return line;
               idx = lf + 1;
            }
            else
               idx = lf + 1; // as lf >= cr
         }
      }

      private SyntaxException(String msg) : base(msg) { }
      private SyntaxException(String msg, Exception inner) : base(msg, inner) { }
   }
}
