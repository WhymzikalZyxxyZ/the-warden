namespace TheWarden.Core.Health;

public readonly record struct DiskUsage(string DriveLetter, long TotalBytes, long FreeBytes)
{
    public long UsedBytes => TotalBytes - FreeBytes;

    public double UsedFraction => TotalBytes == 0 ? 0 : (double)UsedBytes / TotalBytes;
}
