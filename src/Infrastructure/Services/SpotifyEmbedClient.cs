using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Models;
using Core.Services;

namespace Infrastructure.Services;

/// <summary>What Spotify says about a track. Its audio is DRM-protected, so this is all we take from it.</summary>
public sealed record SpotifyTrack(
    string Title,
    IReadOnlyList<string> Artists,
    TimeSpan? Duration,
    DateTimeOffset? ReleasedAt);

/// <summary>
/// Reads a track's metadata from Spotify's public embed page, which carries it as JSON for the
/// player. Needs no API credentials, but the page is undocumented: if Spotify changes its shape,
/// <see cref="ParseEmbedPage"/> (and its fixture test) is the one place to update.
/// </summary>
public sealed partial class SpotifyEmbedClient(HttpClient httpClient)
{
    public async Task<SpotifyTrack> GetTrackAsync(SpotifyUrl url, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"embed/track/{url.TrackId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseEmbedPage(html)
            ?? throw new UserFacingException("Spotify has no track at this link. Check that it opens on open.spotify.com.");
    }

    /// <returns>Null when the page holds no track (Spotify serves its "not found" state with status 200).</returns>
    internal static SpotifyTrack? ParseEmbedPage(string html)
    {
        var match = NextDataScript().Match(html);
        if (!match.Success)
        {
            throw new JsonException("The Spotify embed page has no __NEXT_DATA__ script.");
        }

        using var document = JsonDocument.Parse(match.Groups["json"].Value);
        if (!TryGetPath(document.RootElement, out var entity, "props", "pageProps", "state", "data", "entity")
            || GetString(entity, "type") != "track"
            || (GetString(entity, "name") ?? GetString(entity, "title")) is not { } title)
        {
            return null;
        }

        var artists = entity.TryGetProperty("artists", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(artist => GetString(artist, "name")).OfType<string>().ToList()
            : [];

        return new SpotifyTrack(
            title,
            artists,
            entity.TryGetProperty("duration", out var duration) && duration.TryGetDouble(out var milliseconds)
                ? TimeSpan.FromMilliseconds(milliseconds)
                : null,
            TryGetPath(entity, out var released, "releaseDate", "isoString")
                && released.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(released.GetString(), out var releasedAt)
                ? releasedAt
                : null);
    }

    private static bool TryGetPath(JsonElement element, out JsonElement result, params string[] path)
    {
        result = element;
        foreach (var property in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(property, out result))
            {
                return false;
            }
        }

        return true;
    }

    private static string? GetString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [GeneratedRegex("""<script id="__NEXT_DATA__" type="application/json">(?<json>.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex NextDataScript();
}
