namespace TheWarden.Core.Quarantine;

/// <summary>
/// A quarantine or restore operation failed for a reason the caller can act on
/// (access denied, file in use) rather than a programming error. <see cref="QuarantineManager"/>
/// catches the underlying <see cref="UnauthorizedAccessException"/>/<see cref="IOException"/>
/// from the filesystem call and rethrows this instead, with a message written for display
/// to the end user — callers should not need to inspect the inner exception to react sensibly.
/// </summary>
/// <param name="requiresElevation">
/// True when the failure looks like a permissions problem an admin relaunch would fix —
/// lets the caller offer a "Relaunch as Administrator" action instead of a dead-end
/// message the user can't act on themselves.
/// </param>
public sealed class QuarantineOperationException(string message, Exception inner, bool requiresElevation = false)
    : Exception(message, inner)
{
    public bool RequiresElevation { get; } = requiresElevation;
}
