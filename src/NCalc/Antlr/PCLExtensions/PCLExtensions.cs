#if NETSTANDARD1_3
namespace System
{
    public class SerializableAttribute : Attribute
    {
    }

    public class NonSerializedAttribute : Attribute
    {
    }

    public interface ICloneable
    {
        // Methods
        object Clone();
    }
}

namespace System.Runtime.Serialization
{
    public class SerializationInfo
    {
        internal void AddValue(string p, object _elementDescription)
        {
            throw new NotImplementedException();
        }

        internal string GetString(string p)
        {
            throw new NotImplementedException();
        }

        internal int GetInt32(string p)
        {
            throw new NotImplementedException();
        }

        internal bool GetBoolean(string p)
        {
            throw new NotImplementedException();
        }

        internal object GetValue(string p, Type type)
        {
            throw new NotImplementedException();
        }
    }
}
#endif