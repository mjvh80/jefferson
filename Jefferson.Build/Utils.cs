using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jefferson.Build
{
    public static class Utils
    {
        public static TResult ConvertType<TResult>(Object input, Type type = null)
        {
            // Can prolly do bit better later.

            if (type == null)
                type = typeof(TResult);

            try
            {
                return (TResult)Convert.ChangeType(input, type);
            }
            catch (InvalidCastException)
            {
                // If T is nullable, let's try to convert the underlying type.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return (TResult)ConvertType<Object>(input, typeof(TResult).GetGenericArguments()[0]);
                }

                if (type.IsEnum)
                {
                    return (TResult)Enum.Parse(type, input.ToString(), ignoreCase: true);
                }

                throw;
            }
        }
    }
}
