using System.Net;
using System.Text;
using TheWarden.Core.Reputation;

namespace TheWarden.Core.Tests.Reputation;

public class VirusTotalReputationClientTests
{
    private sealed class FakeHandler(HttpStatusCode statusCode, string? jsonBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(statusCode);
            if (jsonBody is not null)
            {
                response.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }

    private const string SampleHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task LookupAsync_returns_NotChecked_when_hash_is_unknown_to_VirusTotal()
    {
        var handler = new FakeHandler(HttpStatusCode.NotFound, null);
        var client = new VirusTotalReputationClient(new HttpClient(handler), "test-key");

        var verdict = await client.LookupAsync(SampleHash);

        Assert.Equal(ReputationStatus.NotChecked, verdict.Status);
    }

    [Fact]
    public async Task LookupAsync_returns_Malicious_when_any_engine_flags_it()
    {
        var body = """
        {"data":{"attributes":{"last_analysis_stats":{"malicious":3,"suspicious":1,"harmless":60,"undetected":6}}}}
        """;
        var handler = new FakeHandler(HttpStatusCode.OK, body);
        var client = new VirusTotalReputationClient(new HttpClient(handler), "test-key");

        var verdict = await client.LookupAsync(SampleHash);

        Assert.Equal(ReputationStatus.Malicious, verdict.Status);
        Assert.Equal(3, verdict.MaliciousEngineCount);
        Assert.Equal(70, verdict.TotalEngineCount);
    }

    [Fact]
    public async Task LookupAsync_returns_Clean_when_no_engine_flags_it()
    {
        var body = """
        {"data":{"attributes":{"last_analysis_stats":{"malicious":0,"suspicious":0,"harmless":68,"undetected":2}}}}
        """;
        var handler = new FakeHandler(HttpStatusCode.OK, body);
        var client = new VirusTotalReputationClient(new HttpClient(handler), "test-key");

        var verdict = await client.LookupAsync(SampleHash);

        Assert.Equal(ReputationStatus.Clean, verdict.Status);
    }

    [Fact]
    public async Task LookupAsync_sends_api_key_header()
    {
        var body = """
        {"data":{"attributes":{"last_analysis_stats":{"malicious":0,"suspicious":0,"harmless":1,"undetected":0}}}}
        """;
        var handler = new FakeHandler(HttpStatusCode.OK, body);
        var client = new VirusTotalReputationClient(new HttpClient(handler), "test-key");

        await client.LookupAsync(SampleHash);

        Assert.Equal("test-key", handler.LastRequest!.Headers.GetValues("x-apikey").Single());
    }

    [Fact]
    public async Task LookupAsync_returns_LookupFailed_on_server_error()
    {
        var handler = new FakeHandler(HttpStatusCode.InternalServerError, null);
        var client = new VirusTotalReputationClient(new HttpClient(handler), "test-key");

        var verdict = await client.LookupAsync(SampleHash);

        Assert.Equal(ReputationStatus.LookupFailed, verdict.Status);
    }
}
