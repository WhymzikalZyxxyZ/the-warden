namespace TheWarden.Core.Quarantine;

/// <summary>
/// A quarantine or restore operation failed for a reason the caller can act on
/// (access denied, file in use) rather than a programming error. <see cref="QuarantineManager"/>
/// catches the underlying <see cref="UnauthorizedAccessException"/>/<see cref="IOException"/>
/// from the filesystem call and rethrows this instead, with a message written for display
/// to the end user — callers should not need to inspect the inner exception to react sensibly.
/// </summary>
public sealed class QuarantineOperationException(string message, Exception inner)
    : Exception(message, inner);
