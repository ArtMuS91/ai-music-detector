using System.Diagnostics;
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

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        var (exitCode, stdout, stderr) = await RunAsync(workingDirectory, url, timeout.Token);

        if (exitCode != 0)
        {
            logger.LogWarning("yt-dlp exited with {ExitCode} for {VideoId}: {Error}", exitCode, url.VideoId, stderr);
            throw new AudioAcquisitionException($"yt-dlp failed (exit code {exitCode}): {Summarize(stderr)}");
        }

        var filePath = Directory.EnumerateFiles(workingDirectory, $"{url.VideoId}.*").FirstOrDefault()
            ?? throw new AudioAcquisitionException("yt-dlp reported success but produced no audio file.");

        return new AcquiredAudio(filePath, ReadTrackMetadata(stdout, url));
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string workingDirectory,
        YouTubeUrl url,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("--no-playlist");
        startInfo.ArgumentList.Add("--no-progress");
        startInfo.ArgumentList.Add("--quiet");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("bestaudio");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(Path.Combine(workingDirectory, "%(id)s.%(ext)s"));
        startInfo.ArgumentList.Add("--print-json");
        startInfo.ArgumentList.Add(url.CanonicalUrl);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new AudioAcquisitionException(
                $"Could not start '{_options.ExecutablePath}'. Is yt-dlp installed and on PATH?", ex);
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new AudioAcquisitionException($"yt-dlp timed out after {_options.Timeout}.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return (process.ExitCode, await stdout, await stderr);
    }

    private void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not kill the yt-dlp process after cancellation.");
        }
    }

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

    private static string Summarize(string stderr)
    {
        var lastLine = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return string.IsNullOrEmpty(lastLine) ? "no error output" : lastLine;
    }
}
