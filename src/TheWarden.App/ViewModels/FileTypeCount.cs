namespace TheWarden.App.ViewModels;

/// <summary>
/// Pure display data for the malware-scan dashboard's file-type breakdown bars — not a
/// Core concept, just a grouping of what a scan found. BarWidthPixels is computed once
/// here (not in XAML) deliberately: calling a page-level method from inside a
/// DataTemplate's x:Bind needs the item's own binding context to somehow reach back to
/// the outer page, which is exactly the kind of cross-context x:Bind that produced a
/// real compiler bug elsewhere in this app already (see NeedsApiKey). A plain bound
/// property has no such risk.
/// </summary>
public sealed record FileTypeCount(string Extension, int Count)
{
    public double FractionOfTotal { get; init; }
    public double BarWidthPixels { get; init; }
}
