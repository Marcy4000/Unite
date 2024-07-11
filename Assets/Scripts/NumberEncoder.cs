using System;

public static class NumberEncoder
{
    public static string ToBase64<T>(T number) where T : struct
    {
        byte[] bytes;

        if (number is int intVal)
            bytes = BitConverter.GetBytes(intVal);
        else if (number is long longVal)
            bytes = BitConverter.GetBytes(longVal);
        else if (number is float floatVal)
            bytes = BitConverter.GetBytes(floatVal);
        else if (number is double doubleVal)
            bytes = BitConverter.GetBytes(doubleVal);
        else if (number is short shortVal)
            bytes = BitConverter.GetBytes(shortVal);
        else if (number is ushort ushortVal)
            bytes = BitConverter.GetBytes(ushortVal);
        else if (number is uint uintVal)
            bytes = BitConverter.GetBytes(uintVal);
        else if (number is ulong ulongVal)
            bytes = BitConverter.GetBytes(ulongVal);
        else if (number is bool boolVal)
            bytes = BitConverter.GetBytes(boolVal);
        else if (number is char charVal)
            bytes = BitConverter.GetBytes(charVal);
        else
            throw new ArgumentException($"Type {typeof(T)} is not supported");

        return Convert.ToBase64String(bytes);
    }

    public static T FromBase64<T>(string base64) where T : struct
    {
        if (string.IsNullOrEmpty(base64))
        {
            return default;
        }

        byte[] bytes = Convert.FromBase64String(base64);

        if (typeof(T) == typeof(int))
            return (T)(object)BitConverter.ToInt32(bytes, 0);
        else if (typeof(T) == typeof(long))
            return (T)(object)BitConverter.ToInt64(bytes, 0);
        else if (typeof(T) == typeof(float))
            return (T)(object)BitConverter.ToSingle(bytes, 0);
        else if (typeof(T) == typeof(double))
            return (T)(object)BitConverter.ToDouble(bytes, 0);
        else if (typeof(T) == typeof(short))
            return (T)(object)BitConverter.ToInt16(bytes, 0);
        else if (typeof(T) == typeof(ushort))
            return (T)(object)BitConverter.ToUInt16(bytes, 0);
        else if (typeof(T) == typeof(uint))
            return (T)(object)BitConverter.ToUInt32(bytes, 0);
        else if (typeof(T) == typeof(ulong))
            return (T)(object)BitConverter.ToUInt64(bytes, 0);
        else if (typeof(T) == typeof(bool))
            return (T)(object)BitConverter.ToBoolean(bytes, 0);
        else if (typeof(T) == typeof(char))
            return (T)(object)BitConverter.ToChar(bytes, 0);
        else
            throw new ArgumentException($"Type {typeof(T)} is not supported");
    }
}