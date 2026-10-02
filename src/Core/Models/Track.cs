namespace Core.Models;

/// <param name="MatchedUrl">
/// The YouTube video whose audio was analyzed, when the source link has no downloadable audio of
/// its own (Spotify): a best-guess search match, so it may be a different version of the track.
/// </param>
public sealed record Track(
    string SourceUrl,
    string? Title = null,
    string? Artist = null,
    string? Channel = null,
    TimeSpan? Duration = null,
    DateTimeOffset? PublishedAt = null,
    string? Description = null,
    IReadOnlyList<string>? Tags = null,
    string? MatchedUrl = null);
