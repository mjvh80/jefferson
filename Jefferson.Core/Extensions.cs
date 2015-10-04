using System;
using System.Globalization;
using System.Linq.Expressions;

namespace Jefferson.Extensions
{
   internal static class _Extensions
   {
      #region Expression Extensions

      public static Boolean _is_<TType>(this Expression e)
      {
         return typeof(TType).IsAssignableFrom(e.Type);
      }

      public static Expression DynCast<TType>(this Expression e)
      {
         return Expression.Convert(Expression.Convert(e, typeof(Object)), typeof(TType));
      }

      public static Expression Convert<TType>(this Expression e)
      {
         return Expression.Convert(e, typeof(TType));
      }

      //public static Expression OptCast<TType>(this Expression e)
      //{
      //   return e.IfTypeIs<TType>(e.DynCast<TType>(), e);
      //}

      public static Expression IfTypeIs<TType>(this Expression e, Expression then, Expression @else)
      {
         return Expression.Condition(Expression.TypeIs(e, typeof(TType)), then, @else);
      }

      public static Boolean IsNumeric(this Expression e)
      {
         return e.Type.IsNumeric();
      }

      public static Boolean IsIntegral(this Expression e)
      {
         return e.Type.IsIntegral(); 
      }

      public static Boolean IsNullConstant(this Expression e)
      {
         var ex = e as ConstantExpression;
         return ex != null && ex.Value == null;
      }

      #endregion

      public static Boolean _is_<TType>(this Type e)
      {
         return e.IsAssignableFrom(typeof(TType));
      }

      public static Boolean IsIntegral(this Type e)
      {
         return e.IsNumeric() && !(e._is_<Single>() || e._is_<Double>()); // || e._is_<Decimal>());
      }

      public static Boolean IsNumeric(this Type e)
      {
         return e._is_<Char>() || e._is_<SByte>() || e._is_<Byte>() || e._is_<Int16>() || e._is_<UInt16>() ||
                e._is_<Int32>() || e._is_<UInt32>() || e._is_<Int64>() || e._is_<UInt64>() || e._is_<Double>() ||
                e._is_<Single>(); // || e._is_<Decimal>();
      }

      public static Int32 IndexOfWhiteSpace(this String str, Int32 startAt = 0)
      {
         if (String.IsNullOrEmpty(str)) return -1;
         if (startAt < 0 || startAt >= str.Length) return -1;

         // todo: can optimize this
         var i = startAt;
         for (; i < str.Length; i++)
            if (Char.IsWhiteSpace(str[i]))
               return i;

         return -1;
      }

      #region ToStringInvariant

      private const String _kNull = null;

      public static String ToStringInvariant(this DateTime d)
      {
         return d.ToString(DateTimeFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this DateTime? d)
      {
         return d == null ? _kNull : d.Value.ToString(DateTimeFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Int64 v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Int64? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this UInt64 v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this UInt64? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Int32 v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Int32? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this UInt32 v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this UInt32? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Int16 v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Int16? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this UInt16 v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this UInt16? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Double v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Double? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Single v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Single? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Decimal v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Decimal? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Byte v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this Byte? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this SByte v)
      {
         return v.ToString(NumberFormatInfo.InvariantInfo);
      }

      public static String ToStringInvariant(this SByte? v)
      {
         return v == null ? _kNull : v.Value.ToString(NumberFormatInfo.InvariantInfo);
      }

      #endregion
   }
}
