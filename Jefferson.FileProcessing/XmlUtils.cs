using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Jefferson.FileProcessing
{
   internal delegate Exception NodeErrorHandler(XmlNode node, Exception inner, String msg, params String[] args);

   // Todo: needs major cleanup
   internal class XmlUtils
   {
      public static Boolean ReadBool(XmlNode node, String expr, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         return _read(node, expr, new _Converter1<Boolean>(ToBoolean), nsMgr, error);
      }

      public static Boolean ReadBool(XmlNode node, String expr, NodeErrorHandler error)
      {
         return ReadBool(node, expr, null, error);
      }

      public static Boolean OptReadBool(XmlNode node, String expr, Boolean @default, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         return _optRead(node, expr, @default, ToBoolean, nsMgr, error);
      }

      public static Boolean OptReadBool(XmlNode node, String expr, Boolean @default, NodeErrorHandler error)
      {
         return OptReadBool(node, expr, @default, null, error);
      }

      public static String ReadStr(XmlNode node, String expr, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         return _GetNodeValue(node, expr, nsMgr, error);
      }

      public static String ReadStr(XmlNode node, String expr, NodeErrorHandler error)
      {
         return ReadStr(node, expr, null, error);
      }

      public static String OptReadStr(XmlNode node, String expr, String @default, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         String v = _GetOptNodeValue(node, expr, nsMgr, error);
         return (v == null) ? @default : v;
      }

      public static String OptReadStr(XmlNode node, String expr, String @default, NodeErrorHandler error)
      {
         return OptReadStr(node, expr, @default, null, error);
      }

      public static String ReadStrRaw(XmlNode node, String expr, _XmlRawMode mode, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         return _GetNodeValueRaw(node, expr, mode, nsMgr, error);
      }

      public static String ReadStrRaw(XmlNode node, String expr, _XmlRawMode mode, NodeErrorHandler error)
      {
         return ReadStrRaw(node, expr, mode, null, error);
      }

      public static String OptReadStrRaw(XmlNode node, String expr, _XmlRawMode mode, String @default, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         String v = _GetNodeValueRaw(node, expr, mode, nsMgr, error);
         switch (v)
         {
            case null: if ((mode & _XmlRawMode.DefaultOnNull) != 0) v = @default; break;
            case "": if ((mode & _XmlRawMode.DefaultOnEmpty) != 0) v = @default; break;
         }
         return (v == null) ? @default : v;
      }

      public static String OptReadStrRaw(XmlNode node, String expr, _XmlRawMode mode, String @default, NodeErrorHandler error)
      {
         return OptReadStrRaw(node, expr, mode, @default, null, error);
      }

      [Flags]
      internal enum _XmlRawMode
      {
         ExceptNullValue = 1,
         ExceptEmptyValue = 2,
         ExceptNullOrEmpty = 3,
         TrimStart = 4,
         TrimEnd = 8,
         Trim = 12,
         EmptyToNull = 16,
         DefaultOnNull = 32,
         DefaultOnEmpty = 64,
         DefaultOnNullOrEmpty = 96,
      }

      protected delegate T _Converter1<T>(String s, NodeErrorHandler h);

      protected delegate T _Converter2<T>(String s, T def, NodeErrorHandler h);

      private static T _optRead<T>(XmlNode node, String expr, T @default, _Converter2<T> cnv, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         String s = _GetOptNodeValue(node, expr, nsMgr, error);
         try
         {
            return cnv(s, @default, error);
         }
         catch (Exception ee)
         {
            throw error(node, ee, expr);
         }
      }

      protected static T _read<T>(XmlNode node, String expr, _Converter1<T> cnv, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         String s = _GetNodeValue(node, expr, nsMgr, error);
         try
         {
            return cnv(s, error);
         }
         catch (Exception ee)
         {
            if (error == null) throw;
            throw error(node, ee, ee.Message);
         }
      }

      protected static String _GetNodeValue(XmlNode node, String expr, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         return _GetNodeValueRaw(node, expr, _XmlRawMode.EmptyToNull | _XmlRawMode.ExceptNullOrEmpty | _XmlRawMode.Trim, nsMgr, error);
      }

      private static String _GetOptNodeValue(XmlNode node, String expr, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         return _GetNodeValueRaw(node, expr, _XmlRawMode.EmptyToNull | _XmlRawMode.Trim, nsMgr, error);
      }

      protected static String __GetNodeValueRaw(XmlNode node, String expr, XmlNamespaceManager nsMgr)
      {
         if (node == null) return null;

         String tmp = (expr == null) ? null : expr.Trim();
         if (expr == null) return node.InnerText;

         XmlNode v = node.SelectSingleNode(tmp, nsMgr);
         return (v == null) ? null : v.InnerText;
      }

      public static String _GetNodeValueRaw(XmlNode node, String expr, _XmlRawMode mode, XmlNamespaceManager nsMgr, NodeErrorHandler error)
      {
         String tmp = __GetNodeValueRaw(node, expr, nsMgr);
         if (tmp == null)
         {
            if ((mode & _XmlRawMode.ExceptNullValue) != 0) throw error(node, null, "Missing value '{0}'.", expr);
            return tmp;
         }
         switch (mode & _XmlRawMode.Trim)
         {
            case _XmlRawMode.TrimStart: tmp = tmp.TrimStart(' ', '\r', '\n', '\t'); break;
            case _XmlRawMode.TrimEnd: tmp = tmp.TrimEnd(' ', '\r', '\n', '\t'); break;
            case _XmlRawMode.Trim: tmp = tmp.Trim(' ', '\r', '\n', '\t'); break;
         }
         if (tmp == "")
         {
            if ((mode & _XmlRawMode.ExceptEmptyValue) != 0) throw error(node, null, "Empty value '{0}'.", expr);
            if ((mode & _XmlRawMode.EmptyToNull) != 0) tmp = null;
         }
         return tmp;
      }

      #region Misc Utils

      public static String TrimToNull(String s)
      {
         if (String.IsNullOrEmpty(s)) return null;
         String tmp = s.Trim();
         return String.IsNullOrEmpty(tmp) ? null : tmp;
      }

      public static Boolean ToBoolean(String s, Boolean def, NodeErrorHandler error)
      {
         String s2 = TrimToNull(s);
         return (s2 == null) ? def : ToBoolean(s2, error);
      }

      public static Boolean TryToBoolean(String s, out Boolean result)
      {
         return TryToBoolean(s, false, out result);
      }

      public static Boolean TryToBoolean(String s, Boolean @default, out Boolean result)
      {
         s = TrimToNull(s);
         if (s == null)
         {
            result = @default;
            return false;
         }
         s = s.ToUpperInvariant();
         switch (s)
         {
            case "0":
            case "FALSE":
               result = false;
               return true;
            case "1":
            case "-1":
            case "TRUE":
               result = true;
               return true;
         }

         if (!Boolean.TryParse(s, out result))
         {
            result = @default;
            return false;
         }

         return true;
      }

      public static Boolean ToBoolean(String s, NodeErrorHandler error)
      {
         String s2 = TrimToNull(s);
         if (s2 == null) throwEmpty("boolean", error);
         s2 = s2.ToUpperInvariant();
         switch (s2)
         {
            case "0":
            case "FALSE":
               return false;
            case "1":
            case "-1":
            case "TRUE":
               return true;
         }
         try
         {
            return Boolean.Parse(s2);
         }
         catch (Exception e)
         {
            throwInvalid("boolean", s, e, error);
         }
         return false; //Keep compiler happy
      }

      private static void throwEmpty(String t, NodeErrorHandler error)
      {
         throw error(null, null, "Invalid {0}: empty String", t);
      }

      private static void throwInvalid(String t, String s, Exception e, NodeErrorHandler error)
      {
         throw error(null, e, "Invalid {0} '{1}': {2}", t, s, e.Message);
      }

      #endregion
   }
}
