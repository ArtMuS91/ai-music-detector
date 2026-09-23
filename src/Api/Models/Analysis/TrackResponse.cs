using Core.Models;

namespace Api.Models.Analysis;

public sealed record TrackResponse(string? Title, string? Artist, string? Channel, double? DurationSeconds)
{
    public static TrackResponse From(Track track)
        => new(track.Title, track.Artist, track.Channel, track.Duration?.TotalSeconds);
}
