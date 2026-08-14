using TheWarden.Core.Reputation;

namespace TheWarden.Core.Tests.Reputation;

public class DpapiApiKeyStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("warden-apikey-tests-").FullName;
    private readonly string _filePath;

    public DpapiApiKeyStoreTests()
    {
        _filePath = Path.Combine(_root, "apikey.bin");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void GetApiKey_returns_null_when_nothing_has_ever_been_saved()
    {
        var store = new DpapiApiKeyStore(_filePath);

        Assert.Null(store.GetApiKey());
    }

    [Fact]
    public void SetApiKey_then_GetApiKey_round_trips_the_value()
    {
        var store = new DpapiApiKeyStore(_filePath);

        store.SetApiKey("test-vt-api-key-123");

        Assert.Equal("test-vt-api-key-123", store.GetApiKey());
    }

    [Fact]
    public void SetApiKey_persists_across_separate_store_instances()
    {
        var first = new DpapiApiKeyStore(_filePath);
        first.SetApiKey("persisted-key");

        var second = new DpapiApiKeyStore(_filePath);

        Assert.Equal("persisted-key", second.GetApiKey());
    }

    [Fact]
    public void SetApiKey_encrypts_at_rest_rather_than_storing_plaintext()
    {
        var store = new DpapiApiKeyStore(_filePath);

        store.SetApiKey("super-secret-key");

        var onDisk = File.ReadAllText(_filePath, System.Text.Encoding.Latin1);
        Assert.DoesNotContain("super-secret-key", onDisk);
    }

    [Fact]
    public void ClearApiKey_removes_the_file_and_GetApiKey_reports_null_afterward()
    {
        var store = new DpapiApiKeyStore(_filePath);
        store.SetApiKey("to-be-cleared");

        store.ClearApiKey();

        Assert.Null(store.GetApiKey());
        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public void ClearApiKey_is_a_no_op_when_nothing_was_ever_saved()
    {
        var store = new DpapiApiKeyStore(_filePath);

        store.ClearApiKey();
    }

    [Fact]
    public void GetApiKey_returns_null_for_a_corrupted_file_instead_of_throwing()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(_filePath, [1, 2, 3, 4, 5]);
        var store = new DpapiApiKeyStore(_filePath);

        Assert.Null(store.GetApiKey());
    }
}
