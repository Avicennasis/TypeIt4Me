using System;
using System.Security.Cryptography;

namespace TypeIt4Me.Services;

public static class PinHashComparison
{
    public static bool Equal(string? first, string? second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second)) return false;
        try
        {
            byte[] a = Convert.FromBase64String(first);
            byte[] b = Convert.FromBase64String(second);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch (FormatException) { return false; }
    }
}
