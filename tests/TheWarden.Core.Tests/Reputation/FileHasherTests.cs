using TheWarden.Core.Reputation;

namespace TheWarden.Core.Tests.Reputation;

public class FileHasherTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose() => File.Delete(_tempFile);

    [Fact]
    public void ComputeSha256Hex_matches_known_hash_for_empty_content()
    {
        File.WriteAllBytes(_tempFile, []);

        var hash = FileHasher.ComputeSha256Hex(_tempFile);

        // SHA-256 of zero bytes is a well-known constant.
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    [Fact]
    public void ComputeSha256Hex_is_deterministic_for_the_same_content()
    {
        File.WriteAllText(_tempFile, "the warden");

        var first = FileHasher.ComputeSha256Hex(_tempFile);
        var second = FileHasher.ComputeSha256Hex(_tempFile);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }
}
