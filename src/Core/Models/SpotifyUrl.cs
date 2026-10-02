using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Core.Models;

/// <summary>
/// A validated Spotify track link (open.spotify.com, with or without a locale segment, or a
/// <c>spotify:track:</c> URI), reduced to its track id so share links with <c>?si=</c> and the
/// like all normalize to the same job.
/// </summary>
public sealed partial record SpotifyUrl
{
    private SpotifyUrl(string trackId)
    {
        TrackId = trackId;
    }

    public string TrackId { get; }

    public string CanonicalUrl => $"https://open.spotify.com/track/{TrackId}";

    public static bool TryParse(string? input, [NotNullWhen(true)] out SpotifyUrl? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trackId = ExtractTrackId(input.Trim());
        if (trackId is null || !TrackIdPattern().IsMatch(trackId))
        {
            return false;
        }

        result = new SpotifyUrl(trackId);
        return true;
    }

    private static string? ExtractTrackId(string input)
    {
        const string uriPrefix = "spotify:track:";
        if (input.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return input[uriPrefix.Length..];
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !uri.Host.Equals("open.spotify.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        return segments switch
        {
            ["track", var id] => id,
            ["embed", "track", var id] => id,
            // Localized share links: /intl-de/track/{id}.
            [var locale, "track", var id] when locale.StartsWith("intl-", StringComparison.OrdinalIgnoreCase) => id,
            _ => null,
        };
    }

    [GeneratedRegex("^[A-Za-z0-9]{22}$")]
    private static partial Regex TrackIdPattern();
}
