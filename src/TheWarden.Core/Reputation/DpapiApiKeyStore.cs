using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace TheWarden.Core.Reputation;

/// <summary>
/// Encrypts the API key at rest with Windows DPAPI (<see cref="DataProtectionScope.CurrentUser"/>)
/// and stores the ciphertext in a plain file. Deliberately not Windows Credential Locker
/// (<c>PasswordVault</c>) — that API depends on package identity that a portable,
/// non-MSIX-installed .exe (this app's only distribution format, see the download table
/// in README.md) doesn't reliably have. DPAPI has no such dependency: it works for any
/// process running as the same Windows user, packaged or not, which is what "portable"
/// actually requires.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiApiKeyStore(string filePath) : IApiKeyStore
{
    // Scopes the DPAPI protection to this specific use — decrypting the file with the
    // right user account but the wrong entropy still fails, one more thing an attacker
    // reusing this ciphertext elsewhere on the same account would need to get right.
    private static readonly byte[] Entropy = "TheWarden.VirusTotalApiKey"u8.ToArray();

    private readonly string _filePath = filePath;

    public string? GetApiKey()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var encrypted = File.ReadAllBytes(_filePath);
        if (encrypted.Length == 0)
        {
            return null;
        }

        try
        {
            var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            // Ciphertext from a different user account, or corrupted on disk — either way
            // there's no key to recover here. Treated the same as "never set" rather than
            // surfacing a decrypt error the user can't act on.
            return null;
        }
    }

    public void SetApiKey(string apiKey)
    {
        var plaintext = System.Text.Encoding.UTF8.GetBytes(apiKey);
        var encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(_filePath, encrypted);
    }

    public void ClearApiKey()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
