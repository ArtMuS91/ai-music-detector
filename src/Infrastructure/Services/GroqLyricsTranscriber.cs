using System.Net.Http.Headers;
using System.Text.Json;
using Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class LyricsTranscriptionException(string message) : Exception(message);

/// <summary>
/// Transcribes lyrics with Whisper on Groq (<c>audio/transcriptions</c>). Whisper invents text over
/// music it can't make out ("Thank you for watching" over an instrumental is a classic), so
/// segments it scores as probably not speech, or as low-confidence or repetitive, are dropped
/// rather than handed to the explanation as if they were lyrics.
/// </summary>
public sealed class GroqLyricsTranscriber(
    HttpClient http,
    IOptions<GroqOptions> options,
    ILogger<GroqLyricsTranscriber> logger) : ILyricsTranscriber
{
    // Whisper's own fallback thresholds for a segment it should not trust.
    private const double MaxNoSpeechProbability = 0.5;
    private const double MinAverageLogProbability = -1.0;
    private const double MaxCompressionRatio = 2.4;

    /// <summary>Fewer words than this is noise, not lyrics.</summary>
    private const int MinWords = 8;

    /// <summary>Enough for the explanation to quote from without crowding out the signals.</summary>
    private const int MaxLength = 3000;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly GroqOptions _options = options.Value;

    public async Task<string?> TranscribeAsync(PreprocessedAudio audio, CancellationToken cancellationToken = default)
    {
        var size = new FileInfo(audio.FilePath).Length;
        if (size > _options.MaxTranscriptionUploadMegabytes * 1024L * 1024L)
        {
            throw new LyricsTranscriptionException(
                $"The audio is {size / (1024 * 1024)} MB, over Groq's {_options.MaxTranscriptionUploadMegabytes} MB transcription limit.");
        }

        var body = await GroqRequests.PostAsync(
            http,
            _options,
            "audio/transcriptions",
            () => BuildForm(audio.FilePath),
            message => new LyricsTranscriptionException(message),
            logger,
            cancellationToken);

        var transcription = JsonSerializer.Deserialize<Transcription>(body, JsonOptions)
            ?? throw new LyricsTranscriptionException("Groq returned an empty transcription.");

        return KeepLikelyLyrics(transcription);
    }

    private MultipartFormDataContent BuildForm(string path)
    {
        var file = new StreamContent(File.OpenRead(path));
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        return new MultipartFormDataContent
        {
            { file, "file", Path.GetFileName(path) },
            { new StringContent(_options.TranscriptionModel), "model" },
            // verbose_json carries the per-segment confidence that KeepLikelyLyrics filters on.
            { new StringContent("verbose_json"), "response_format" },
            { new StringContent("0"), "temperature" },
        };
    }

    private string? KeepLikelyLyrics(Transcription transcription)
    {
        var segments = transcription.Segments ?? [];
        var lines = segments
            .Where(s => s.NoSpeechProb < MaxNoSpeechProbability
                && s.AvgLogprob > MinAverageLogProbability
                && s.CompressionRatio < MaxCompressionRatio)
            .Select(s => s.Text?.Trim())
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();

        var lyrics = string.Join("\n", lines);
        var words = lyrics.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        // The thresholds are Whisper's defaults, not tuned on music; this shows how much they let through.
        logger.LogInformation(
            "Whisper kept {Kept} of {Total} segments ({Words} words, language {Language}).",
            lines.Count,
            segments.Count,
            words,
            transcription.Language);

        if (words < MinWords)
        {
            return null;
        }

        return lyrics.Length <= MaxLength
            ? lyrics
            : lyrics[..MaxLength] + "…";
    }

    private sealed record Transcription(string? Text, string? Language, IReadOnlyList<Segment>? Segments);

    private sealed record Segment(string? Text, double NoSpeechProb, double AvgLogprob, double CompressionRatio);
}
