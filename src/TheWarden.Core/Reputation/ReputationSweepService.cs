using System.Runtime.CompilerServices;

namespace TheWarden.Core.Reputation;

/// <summary>
/// Hashes and checks each file's reputation one at a time, rate-limited between VirusTotal
/// calls to protect the user's API key from tripping the public API's request limits.
/// Streams a MalwareFinding for every file it's given — including ones it couldn't hash —
/// so a caller tracking "checked N of Total" can just count yields rather than needing its
/// own separate bookkeeping for skipped files.
/// </summary>
public sealed class ReputationSweepService(IReputationClient client, TimeSpan delayBetweenChecks)
{
    public async IAsyncEnumerable<MalwareFinding> SweepAsync(
        IReadOnlyList<Scanning.FileEntry> files,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];

            // yield can't appear inside a try that has a catch, so the try only ever
            // sets a plain local — every yield in this method happens outside it.
            string? sha256 = null;
            var hashFailed = false;
            try
            {
                sha256 = await Task.Run(() => FileHasher.ComputeSha256Hex(file.FullPath), cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                hashFailed = true;
            }

            if (hashFailed)
            {
                yield return new MalwareFinding(file.FullPath, file.SizeBytes, new ReputationVerdict(string.Empty, ReputationStatus.LookupFailed, 0, 0));
            }
            else
            {
                var verdict = await client.LookupAsync(sha256!, cancellationToken);
                yield return new MalwareFinding(file.FullPath, file.SizeBytes, verdict);
            }

            // No point waiting out the rate limit after the very last file.
            if (i < files.Count - 1)
            {
                await Task.Delay(delayBetweenChecks, cancellationToken);
            }
        }
    }
}
