using System.Security.Cryptography;
using System.Text;

namespace Aahar.Infrastructure;

public static class GuestAccessToken
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.ASCII.GetBytes(token));

    public static bool IsValidFormat(string? token) =>
        token is { Length: 64 } && token.All(char.IsAsciiHexDigit);

    public static bool Matches(string token, byte[] hash) =>
        IsValidFormat(token) && CryptographicOperations.FixedTimeEquals(hash, Hash(token));
}
