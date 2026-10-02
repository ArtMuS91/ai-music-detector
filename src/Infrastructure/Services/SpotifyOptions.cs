namespace Infrastructure.Services;

public sealed class SpotifyOptions
{
    public const string SectionName = "Spotify";

    /// <summary>Host of the public embed pages (<c>embed/track/{id}</c>) track metadata is read from.</summary>
    public Uri BaseUrl { get; set; } = new("https://open.spotify.com/");

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
