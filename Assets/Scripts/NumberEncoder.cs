using System;

public static class NumberEncoder
{
    public static string IntToBase64(int number)
    {
        byte[] bytes = BitConverter.GetBytes(number);
        return Convert.ToBase64String(bytes);
    }

    public static string ShortToBase64(short number)
    {
        byte[] bytes = BitConverter.GetBytes(number);
        return Convert.ToBase64String(bytes);
    }

    public static int Base64ToInt(string base64String)
    {
        if (string.IsNullOrEmpty(base64String))
        {
            return 0;
        }

        byte[] bytes = Convert.FromBase64String(base64String);

        if (bytes.Length != 4)
        {
            return 0;
        }

        return BitConverter.ToInt32(bytes, 0);
    }

    public static short Base64ToShort(string base64String)
    {
        if (string.IsNullOrEmpty(base64String))
        {
            return 0;
        }

        byte[] bytes = Convert.FromBase64String(base64String);

        if (bytes.Length != 2)
        {
            return 0;
        }

        return BitConverter.ToInt16(bytes, 0);
    }
}