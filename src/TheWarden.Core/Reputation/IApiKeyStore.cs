namespace TheWarden.Core.Reputation;

/// <summary>Persists the user-supplied VirusTotal API key between app launches.</summary>
public interface IApiKeyStore
{
    string? GetApiKey();
    void SetApiKey(string apiKey);
    void ClearApiKey();
}
