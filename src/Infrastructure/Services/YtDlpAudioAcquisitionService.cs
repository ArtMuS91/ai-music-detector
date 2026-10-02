using System.Text.Json;
using Core.Models;
using Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class AudioAcquisitionException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Downloads the best available audio-only stream with yt-dlp. No transcoding happens
/// here — the preprocessing stage owns format normalization, so acquisition stays
/// free of an ffmpeg dependency.
/// </summary>
/// <remarks>
/// Spotify's audio is DRM-protected, so a Spotify link is resolved to its title, artists and
/// duration, and the audio comes from the best-matching YouTube video instead.
/// </remarks>
public sealed class YtDlpAudioAcquisitionService(
    IOptions<YtDlpOptions> options,
    SpotifyEmbedClient spotify,
    ILogger<YtDlpAudioAcquisitionService> logger) : IAudioAcquisitionService
{
    /// <summary>How many YouTube search results a Spotify track is matched against.</summary>
    internal const int SearchResultCount = 5;

    /// <summary>A search result this close to the Spotify duration is taken to be the same recording.</summary>
    internal static readonly TimeSpan MatchDurationTolerance = TimeSpan.FromSeconds(5);

    private readonly YtDlpOptions _options = options.Value;

    public Task<AcquiredAudio> AcquireAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (YouTubeUrl.TryParse(sourceUrl, out var youTubeUrl))
        {
            return DownloadAsync(youTubeUrl, cancellationToken);
        }

        if (SpotifyUrl.TryParse(sourceUrl, out var spotifyUrl))
        {
            return AcquireFromSpotifyAsync(spotifyUrl, cancellationToken);
        }

        throw new AudioAcquisitionException($"'{sourceUrl}' is not a YouTube or Spotify track link.");
    }

    private async Task<AcquiredAudio> AcquireFromSpotifyAsync(SpotifyUrl url, CancellationToken cancellationToken)
    {
        SpotifyTrack spotifyTrack;
        try
        {
            spotifyTrack = await spotify.GetTrackAsync(url, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Could not read Spotify track {TrackId}.", url.TrackId);
            throw new UserFacingException("Could not read the track's details from Spotify. Try again in a moment.");
        }

        var query = SearchQuery(spotifyTrack);
        var candidates = await SearchYouTubeAsync(query, cancellationToken);
        var match = PickMatch(candidates, spotifyTrack.Duration, _options.MaxDuration)
            ?? throw new UserFacingException($"No YouTube video was found to analyze for \"{query}\".");

        logger.LogInformation(
            "Spotify track {TrackId} matched to YouTube video {VideoId} ({MatchedDuration} vs {SpotifyDuration}).",
            url.TrackId,
            match.Url.VideoId,
            match.Duration,
            spotifyTrack.Duration);

        var acquired = await DownloadAsync(match.Url, cancellationToken);
        return acquired with { Track = MergeSpotifyTrack(acquired.Track, spotifyTrack, url) };
    }

    private async Task<AcquiredAudio> DownloadAsync(YouTubeUrl url, CancellationToken cancellationToken)
    {

        // Each download gets its own folder, so a failed or cancelled run's partial files
        // (.part, .ytdl) can be removed wholesale, and no stale file from an earlier job can
        // be mistaken for this one's output.
        var downloadDirectory = Path.Combine(WorkingDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(downloadDirectory);

        try
        {
            var result = await ExternalProcess.RunAsync(
                _options.ExecutablePath,
                BuildArguments(downloadDirectory, url),
                _options.Timeout,
                logger,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                logger.LogWarning("yt-dlp exited with {ExitCode} for {VideoId}: {Error}", result.ExitCode, url.VideoId, result.Stderr);
                throw new AudioAcquisitionException(
                    $"yt-dlp failed (exit code {result.ExitCode}): {ExternalProcess.Summarize(result.Stderr)}");
            }

            // yt-dlp exits successfully without downloading when --match-filter or --max-filesize rejects the video.
            var filePath = Directory.EnumerateFiles(downloadDirectory, $"{url.VideoId}.*").FirstOrDefault()
                ?? throw new UserFacingException(
                    $"The video was skipped: live streams, videos longer than {_options.MaxDuration.TotalMinutes:0} minutes "
                    + $"and audio larger than {_options.MaxFileSizeMegabytes} MB are not analyzed.");

            return new AcquiredAudio(filePath, ReadTrackMetadata(result.Stdout, url));
        }
        catch
        {
            TryDeleteDirectory(downloadDirectory);
            throw;
        }
    }

    public void DeleteDownload(string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));

        if (directory is not null && IsDownloadDirectory(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
        else
        {
            File.Delete(filePath);
        }
    }

    internal static string SearchQuery(SpotifyTrack track)
        => track.Artists.Count > 0
            ? $"{string.Join(", ", track.Artists)} - {track.Title}"
            : track.Title;

    /// <summary>
    /// The first search result (YouTube ranks the official upload high) within
    /// <see cref="MatchDurationTolerance"/> of the Spotify duration, or, failing that, the first
    /// result at all: a best guess the UI flags by showing which video was analyzed.
    /// </summary>
    internal static SearchCandidate? PickMatch(
        IReadOnlyList<SearchCandidate> candidates,
        TimeSpan? spotifyDuration,
        TimeSpan maxDuration)
    {
        // Live streams and longer videos would only be skipped by the download's own filter.
        var eligible = candidates
            .Where(candidate => !candidate.IsLive && !(candidate.Duration > maxDuration))
            .ToList();

        return eligible.FirstOrDefault(candidate => spotifyDuration is { } expected
                && candidate.Duration is { } actual
                && (actual - expected).Duration() <= MatchDurationTolerance)
            ?? eligible.FirstOrDefault();
    }

    /// <summary>
    /// Title, artists and duration come from Spotify, which names the track cleanly; channel,
    /// description, tags and upload date stay those of the matched video. Spotify's release date
    /// is deliberately not used as <see cref="Track.PublishedAt"/>: the distributor picks it, so it
    /// can be backdated, unlike a YouTube upload date.
    /// </summary>
    internal static Track MergeSpotifyTrack(Track matched, SpotifyTrack spotifyTrack, SpotifyUrl url) => matched with
    {
        SourceUrl = url.CanonicalUrl,
        MatchedUrl = matched.SourceUrl,
        Title = spotifyTrack.Title,
        Artist = spotifyTrack.Artists.Count > 0 ? string.Join(", ", spotifyTrack.Artists) : matched.Artist,
        Duration = spotifyTrack.Duration ?? matched.Duration,
    };

    internal sealed record SearchCandidate(YouTubeUrl Url, TimeSpan? Duration, bool IsLive);

    private async Task<IReadOnlyList<SearchCandidate>> SearchYouTubeAsync(string query, CancellationToken cancellationToken)
    {
        var result = await ExternalProcess.RunAsync(
            _options.ExecutablePath,
            [
                "--flat-playlist",
                "--dump-single-json",
                "--no-warnings",
                $"ytsearch{SearchResultCount}:{query}",
            ],
            _options.Timeout,
            logger,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            logger.LogWarning("yt-dlp search exited with {ExitCode} for {Query}: {Error}", result.ExitCode, query, result.Stderr);
            throw new AudioAcquisitionException(
                $"yt-dlp search failed (exit code {result.ExitCode}): {ExternalProcess.Summarize(result.Stderr)}");
        }

        using var document = JsonDocument.Parse(result.Stdout);
        return SearchCandidatesFrom(document.RootElement);
    }

    /// <summary>Maps yt-dlp's <c>--flat-playlist --dump-single-json</c> output for a <c>ytsearchN:</c> query.</summary>
    internal static IReadOnlyList<SearchCandidate> SearchCandidatesFrom(JsonElement root)
    {
        if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var candidates = new List<SearchCandidate>();
        foreach (var entry in entries.EnumerateArray())
        {
            // Only videos have a watch URL; anything else a search returns is skipped.
            if (!YouTubeUrl.TryParse(GetString(entry, "url"), out var url))
            {
                continue;
            }

            candidates.Add(new SearchCandidate(
                url,
                entry.TryGetProperty("duration", out var duration) && duration.TryGetDouble(out var seconds)
                    ? TimeSpan.FromSeconds(seconds)
                    : null,
                GetString(entry, "live_status") is "is_live" or "is_upcoming"));
        }

        return candidates;
    }

    private string WorkingDirectory => Path.GetFullPath(
        _options.WorkingDirectory ?? Path.Combine(Path.GetTempPath(), "ai-music-detector", "audio"));

    /// <summary>
    /// Only a per-download folder directly under the working directory is removed whole — never
    /// the working directory itself (paths saved before downloads had their own folder).
    /// </summary>
    private bool IsDownloadDirectory(string directory)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(directory) ?? ""),
            Path.TrimEndingDirectorySeparator(WorkingDirectory),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete download folder {Directory}.", directory);
        }
    }

    private IEnumerable<string> BuildArguments(string downloadDirectory, YouTubeUrl url) =>
    [
        "--no-playlist",
        "--no-progress",
        "--quiet",
        "--format", "bestaudio",
        "--match-filter", $"!is_live & duration <= {(int)_options.MaxDuration.TotalSeconds}",
        "--max-filesize", $"{_options.MaxFileSizeMegabytes}M",
        "--output", Path.Combine(downloadDirectory, "%(id)s.%(ext)s"),
        "--print-json",
        url.CanonicalUrl,
    ];

    private Track ReadTrackMetadata(string stdout, YouTubeUrl url)
    {
        var json = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.StartsWith('{'));

        if (json is null)
        {
            logger.LogWarning("yt-dlp produced no metadata for {VideoId}; continuing without it.", url.VideoId);
            return new Track(url.CanonicalUrl);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return TrackFromMetadata(document.RootElement, url.CanonicalUrl);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse yt-dlp metadata for {VideoId}; continuing without it.", url.VideoId);
            return new Track(url.CanonicalUrl);
        }
    }

    /// <summary>Maps one object of yt-dlp's <c>--print-json</c> / <c>--dump-json</c> output.</summary>
    internal static Track TrackFromMetadata(JsonElement root, string sourceUrl) => new(
        sourceUrl,
        Title: GetString(root, "track") ?? GetString(root, "title"),
        Artist: GetString(root, "artist") ?? GetString(root, "uploader"),
        Channel: GetString(root, "channel") ?? GetString(root, "uploader"),
        Duration: root.TryGetProperty("duration", out var duration) && duration.TryGetDouble(out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null,
        PublishedAt: ParseUploadDate(GetString(root, "upload_date")),
        Description: GetString(root, "description"),
        Tags: GetStrings(root, "tags"));

    private static string? GetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string>? GetStrings(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToList()
            : null;

    private static DateTimeOffset? ParseUploadDate(string? uploadDate)
        => DateTimeOffset.TryParseExact(
            uploadDate,
            "yyyyMMdd",
            null,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
}
