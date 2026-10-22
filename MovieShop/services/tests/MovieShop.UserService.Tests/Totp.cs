using System.Security.Cryptography;

namespace MovieShop.UserService.Tests;

/// <summary>
/// RFC 6238 TOTP-kódgenerátor — azt számolja ki, amit egy authenticator alkalmazás
/// (pl. Google Authenticator) mutatna a megosztott kulcsból.
/// </summary>
internal static class Totp
{
    public static string Compute(string base32Key, DateTimeOffset? at = null)
    {
        var key = Base32Decode(base32Key.Replace(" ", "").ToUpperInvariant());
        var counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;

        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];

        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        int buffer = 0, bits = 0;

        foreach (var c in input.TrimEnd('='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }

        return output.ToArray();
    }
}
