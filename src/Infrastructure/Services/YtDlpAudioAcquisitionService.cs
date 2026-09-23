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

        var workingDirectory = _options.WorkingDirectory
            ?? Path.Combine(Path.GetTempPath(), "ai-music-detector", "audio");
        Directory.CreateDirectory(workingDirectory);

        var result = await ExternalProcess.RunAsync(
            _options.ExecutablePath,
            BuildArguments(workingDirectory, url),
            _options.Timeout,
            logger,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            logger.LogWarning("yt-dlp exited with {ExitCode} for {VideoId}: {Error}", result.ExitCode, url.VideoId, result.Stderr);
            throw new AudioAcquisitionException(
                $"yt-dlp failed (exit code {result.ExitCode}): {ExternalProcess.Summarize(result.Stderr)}");
        }

        var filePath = Directory.EnumerateFiles(workingDirectory, $"{url.VideoId}.*").FirstOrDefault()
            ?? throw new AudioAcquisitionException("yt-dlp reported success but produced no audio file.");

        return new AcquiredAudio(filePath, ReadTrackMetadata(result.Stdout, url));
    }

    private static IEnumerable<string> BuildArguments(string workingDirectory, YouTubeUrl url) =>
    [
        "--no-playlist",
        "--no-progress",
        "--quiet",
        "--format", "bestaudio",
        "--output", Path.Combine(workingDirectory, "%(id)s.%(ext)s"),
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
