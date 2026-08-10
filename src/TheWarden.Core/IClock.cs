using System.Diagnostics.CodeAnalysis;

namespace TheWarden.Core;

/// <summary>Abstracts wall-clock time so age-based logic (junk age, quarantine retention) is deterministically testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

[ExcludeFromCodeCoverage]
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
