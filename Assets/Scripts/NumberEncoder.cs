using System;

public static class NumberEncoder
{
    // Convert a decimal number to a Base64-encoded string
    public static string DecimalToBase64(int number)
    {
        byte[] bytes = BitConverter.GetBytes(number);
        return Convert.ToBase64String(bytes);
    }

    // Convert a Base64-encoded string back to a decimal number
    public static int Base64ToDecimal(string base64String)
    {
        byte[] bytes = Convert.FromBase64String(base64String);
        return BitConverter.ToInt32(bytes, 0);
    }
}