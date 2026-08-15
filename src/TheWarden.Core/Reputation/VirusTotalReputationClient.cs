using System.Net;
using System.Text.Json;

namespace TheWarden.Core.Reputation;

/// <summary>
/// Optional, opt-in hash-reputation lookup against the VirusTotal public API v3.
/// The API key is supplied by the user (stored via Windows Credential Locker at the
/// app layer, never bundled or hardcoded here) — this class never runs unless the
/// caller explicitly provides one. See ADR-0002 for why this is a flag, not an
/// automatic deletion trigger.
/// </summary>
public sealed class VirusTotalReputationClient(HttpClient httpClient, string apiKey) : IReputationClient
{
    private const string BaseUrl = "https://www.virustotal.com/api/v3/files/";

    private readonly HttpClient _httpClient = httpClient;
    private readonly string _apiKey = apiKey;

    public async Task<ReputationVerdict> LookupAsync(string sha256, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + sha256);
        request.Headers.Add("x-apikey", _apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new ReputationVerdict(sha256, ReputationStatus.LookupFailed, 0, 0);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The ambient token wasn't cancelled, so this wasn't the caller stopping the
            // sweep — it's HttpClient's own request timeout firing. That's a per-file
            // failure, not a reason to take down the whole scan, so it's reported the
            // same way a network error would be rather than rethrown.
            return new ReputationVerdict(sha256, ReputationStatus.LookupFailed, 0, 0);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // VirusTotal has never seen this hash. Absence of data is not the same as
            // a clean verdict, so this is surfaced as "not checked", not "safe".
            return ReputationVerdict.NotChecked(sha256);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new ReputationVerdict(sha256, ReputationStatus.LookupFailed, 0, 0);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var stats = document.RootElement
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("last_analysis_stats");

        var malicious = stats.GetProperty("malicious").GetInt32();
        var suspicious = stats.GetProperty("suspicious").GetInt32();
        var harmless = stats.GetProperty("harmless").GetInt32();
        var undetected = stats.GetProperty("undetected").GetInt32();
        var total = malicious + suspicious + harmless + undetected;

        var status = malicious > 0
            ? ReputationStatus.Malicious
            : suspicious > 0
                ? ReputationStatus.Suspicious
                : ReputationStatus.Clean;

        return new ReputationVerdict(sha256, status, malicious, total);
    }
}
