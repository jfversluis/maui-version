namespace MauiVersion.Models;

public enum ReleaseChannelType
{
    Stable,
    Nightly,
    PR
}

public class ReleaseChannel
{
    public required string Name { get; init; }
    public required ReleaseChannelType Type { get; init; }
    public required string Description { get; init; }
    public string? FeedUrl { get; init; }
    public int? PrNumber { get; init; }
}
