using System.Buffers.Binary;
using System.Globalization;
using Core.Models;
using Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class AudioPreprocessingException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Normalizes whatever container/codec yt-dlp downloaded into one predictable shape for
/// the detectors: 16-bit PCM WAV, mono, fixed sample rate, capped to a window from the
/// middle of the track. The output is written next to the source file.
/// </summary>
public sealed class FfmpegAudioPreprocessor(
    IOptions<FfmpegOptions> options,
    ILogger<FfmpegAudioPreprocessor> logger) : IAudioPreprocessor
{
    private const int Channels = 1;

    private readonly FfmpegOptions _options = options.Value;

    public async Task<PreprocessedAudio> PreprocessAsync(AcquiredAudio audio, CancellationToken cancellationToken = default)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(audio.FilePath)!,
            $"{Path.GetFileNameWithoutExtension(audio.FilePath)}.preprocessed.wav");

        AudioWindow? window = _options.MaxDuration is { } maxDuration
            ? AudioWindow.Centered(audio.Track.Duration, maxDuration)
            : null;

        try
        {
            var result = await ExternalProcess.RunAsync(
                _options.ExecutablePath,
                BuildArguments(audio.FilePath, outputPath, window),
                _options.Timeout,
                logger,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                logger.LogWarning("ffmpeg exited with {ExitCode} for {Input}: {Error}", result.ExitCode, audio.FilePath, result.Stderr);
                throw new AudioPreprocessingException(
                    $"ffmpeg failed (exit code {result.ExitCode}): {ExternalProcess.Summarize(result.Stderr)}");
            }

            var duration = ReadWavDuration(outputPath);
            if (duration <= TimeSpan.Zero)
            {
                throw new AudioPreprocessingException("ffmpeg produced no audio samples.");
            }

            return new PreprocessedAudio(outputPath, _options.SampleRate, Channels, duration);
        }
        catch
        {
            // The caller never learns the path of a failed output, so it cannot clean it up.
            TryDelete(outputPath);
            throw;
        }
    }

    private List<string> BuildArguments(string inputPath, string outputPath, AudioWindow? window)
    {
        List<string> arguments = ["-nostdin", "-hide_banner", "-loglevel", "error", "-y"];

        if (window is { } w)
        {
            // Before -i, -ss seeks the input instead of decoding and discarding everything up to the offset.
            arguments.AddRange(["-ss", FormatSeconds(w.Start), "-i", inputPath, "-t", FormatSeconds(w.Length)]);
        }
        else
        {
            arguments.AddRange(["-i", inputPath]);
        }

        arguments.AddRange(
        [
            "-vn",
            "-ac", Channels.ToString(CultureInfo.InvariantCulture),
            "-ar", _options.SampleRate.ToString(CultureInfo.InvariantCulture),
            "-c:a", "pcm_s16le",
            // Keeps the WAV header to the plain RIFF/fmt/data chunks ReadWavDuration expects.
            "-map_metadata", "-1",
            "-bitexact",
            outputPath,
        ]);

        return arguments;
    }

    private static string FormatSeconds(TimeSpan value)
        => value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Duration from the WAV header: data chunk size divided by the fmt chunk's byte rate.</summary>
    private static TimeSpan ReadWavDuration(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        stream.ReadExactly(header);

        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
        {
            throw new AudioPreprocessingException("ffmpeg output is not a WAV file.");
        }

        Span<byte> chunkHeader = stackalloc byte[8];
        Span<byte> fmt = stackalloc byte[16];
        uint byteRate = 0;

        while (stream.Read(chunkHeader) == chunkHeader.Length)
        {
            var chunkId = chunkHeader[..4];
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);

            if (chunkId.SequenceEqual("data"u8) && byteRate > 0)
            {
                return TimeSpan.FromSeconds((double)chunkSize / byteRate);
            }

            if (chunkId.SequenceEqual("fmt "u8))
            {
                stream.ReadExactly(fmt);
                byteRate = BinaryPrimitives.ReadUInt32LittleEndian(fmt[8..12]);
                chunkSize -= (uint)fmt.Length;
            }

            // RIFF chunks are word-aligned: odd sizes carry one pad byte.
            stream.Seek(chunkSize + (chunkSize & 1), SeekOrigin.Current);
        }

        throw new AudioPreprocessingException("ffmpeg output has no readable WAV data chunk.");
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete partial preprocessing output {Path}.", path);
        }
    }
}
