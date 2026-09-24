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
public sealed class YtDlpAudioAcquisitionService(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpAudioAcquisitionService> logger) : IAudioAcquisitionService
{
    private readonly YtDlpOptions _options = options.Value;

    public async Task<AcquiredAudio> AcquireAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (!YouTubeUrl.TryParse(sourceUrl, out var url))
        {
            throw new AudioAcquisitionException($"'{sourceUrl}' is not a YouTube video link.");
        }

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
            var root = JsonDocument.Parse(json).RootElement;

            return new Track(
                url.CanonicalUrl,
                Title: GetString(root, "track") ?? GetString(root, "title"),
                Artist: GetString(root, "artist") ?? GetString(root, "uploader"),
                Channel: GetString(root, "channel") ?? GetString(root, "uploader"),
                Duration: root.TryGetProperty("duration", out var duration) && duration.TryGetDouble(out var seconds)
                    ? TimeSpan.FromSeconds(seconds)
                    : null,
                PublishedAt: ParseUploadDate(GetString(root, "upload_date")));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not parse yt-dlp metadata for {VideoId}; continuing without it.", url.VideoId);
            return new Track(url.CanonicalUrl);
        }
    }

    private static string? GetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
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
