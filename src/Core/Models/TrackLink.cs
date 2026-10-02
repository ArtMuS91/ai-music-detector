using System.Diagnostics.CodeAnalysis;

namespace Core.Models;

/// <summary>The link kinds a user may submit, each reduced to one canonical URL per track.</summary>
public static class TrackLink
{
    public static bool TryGetCanonicalUrl(string? input, [NotNullWhen(true)] out string? canonicalUrl)
    {
        canonicalUrl = YouTubeUrl.TryParse(input, out var youTube) ? youTube.CanonicalUrl
            : SpotifyUrl.TryParse(input, out var spotify) ? spotify.CanonicalUrl
            : null;

        return canonicalUrl is not null;
    }
}
