namespace Core.Models;

public sealed record Track(
    string SourceUrl,
    string? Title = null,
    string? Artist = null,
    string? Channel = null,
    TimeSpan? Duration = null,
    DateTimeOffset? PublishedAt = null);
