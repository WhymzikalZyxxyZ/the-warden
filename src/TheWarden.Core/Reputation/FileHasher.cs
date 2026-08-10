using System.Security.Cryptography;

namespace TheWarden.Core.Reputation;

public static class FileHasher
{
    public static string ComputeSha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
