namespace TheWarden.Core.Reputation;

/// <summary>
/// Looks up a file hash's reputation. Opt-in only — see <see cref="VirusTotalReputationClient"/>
/// for the concrete implementation, which requires a user-supplied API key and never
/// runs unless the user has explicitly configured one.
/// </summary>
public interface IReputationClient
{
    Task<ReputationVerdict> LookupAsync(string sha256, CancellationToken cancellationToken = default);
}
