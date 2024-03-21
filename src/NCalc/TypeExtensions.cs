using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace System
{
    public static class TypeExtensions
    {
        private static readonly Dictionary<Type, TypeCode> TypeCodeMap =
            new Dictionary<Type, TypeCode>
            {
                {typeof(bool), TypeCode.Boolean},
                {typeof(byte), TypeCode.Byte},
                {typeof(sbyte), TypeCode.SByte},
                {typeof(char), TypeCode.Char},
                {typeof(DateTime), TypeCode.DateTime},
                {typeof(decimal), TypeCode.Decimal},
                {typeof(double), TypeCode.Double},
                {typeof(float), TypeCode.Single},
                {typeof(short), TypeCode.Int16},
                {typeof(int), TypeCode.Int32},
                {typeof(long), TypeCode.Int64},
                {typeof(ushort), TypeCode.UInt16},
                {typeof(uint), TypeCode.UInt32},
                {typeof(ulong), TypeCode.UInt64},
                {typeof(string), TypeCode.String}
            };

        public static TypeCode GetTypeCode(this object obj)
        {
            if (obj == null)
                return TypeCode.Empty;

            return obj.GetType().ToTypeCode();
        }
        public static TypeCode ToTypeCode(this Type type)
        {
            if (type == null)
                return TypeCode.Empty;

            if (!TypeCodeMap.TryGetValue(type, out TypeCode tc))
            {
                tc = TypeCode.Object;
            }

            return tc;
        }
    }
}
