using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Web;

namespace Core.Models;

/// <summary>
/// A validated YouTube / YouTube Music link, reduced to its video id so that the
/// many URL shapes users paste (share links, Music links, extra playlist params)
/// all normalize to the same job.
/// </summary>
public sealed partial record YouTubeUrl
{
    private YouTubeUrl(string videoId)
    {
        VideoId = videoId;
    }

    public string VideoId { get; }

    public string CanonicalUrl => $"https://www.youtube.com/watch?v={VideoId}";

    public static bool TryParse(string? input, [NotNullWhen(true)] out YouTubeUrl? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(input)
            || !Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var videoId = ExtractVideoId(uri);
        if (videoId is null || !VideoIdPattern().IsMatch(videoId))
        {
            return false;
        }

        result = new YouTubeUrl(videoId);
        return true;
    }

    private static string? ExtractVideoId(Uri uri)
    {
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;

        if (host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.Trim('/');
        }

        if (!host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            && !host.Equals("music.youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length == 2 && segments[0] is "shorts" or "embed" or "v")
        {
            return segments[1];
        }

        return segments is ["watch"]
            ? HttpUtility.ParseQueryString(uri.Query)["v"]
            : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdPattern();
}
