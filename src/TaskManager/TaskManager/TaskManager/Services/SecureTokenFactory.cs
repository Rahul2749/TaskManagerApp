using System.Security.Cryptography;
using System.Text;

namespace TaskManager.Services;

internal static class SecureTokenFactory
{
    public static (string Raw, string Hash) Create()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return (raw, Hash(raw));
    }

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
