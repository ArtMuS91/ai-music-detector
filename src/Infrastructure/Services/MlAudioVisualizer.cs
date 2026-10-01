using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Models;
using Core.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>Asks the Python <c>ml/</c> service (<c>POST /visualize</c>) for the waveform and spectrogram.</summary>
public sealed class MlAudioVisualizer(HttpClient http, ILogger<MlAudioVisualizer> logger) : IAudioVisualizer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<AudioVisualization> VisualizeAsync(PreprocessedAudio audio, CancellationToken cancellationToken = default)
    {
        await using var file = File.OpenRead(audio.FilePath);
        using var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using var form = new MultipartFormDataContent { { fileContent, "audio", Path.GetFileName(audio.FilePath) } };

        using var response = await http.PostAsync("visualize", form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("ML visualization returned {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new MlServiceException($"ML visualization failed with status {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<VisualizationResponse>(JsonOptions, cancellationToken)
            ?? throw new MlServiceException("ML visualization returned an empty response.");
        var spectrogram = result.Spectrogram
            ?? throw new MlServiceException("ML visualization returned no spectrogram.");

        if (spectrogram.Frames <= 0 || spectrogram.Bands <= 0 || spectrogram.Values.Length != spectrogram.Frames * spectrogram.Bands)
        {
            throw new MlServiceException(
                $"ML visualization returned {spectrogram.Values.Length} spectrogram values for {spectrogram.Frames} x {spectrogram.Bands}.");
        }

        return new AudioVisualization(
            audio.Start,
            TimeSpan.FromSeconds(result.DurationSeconds),
            [.. (result.Waveform ?? []).Select(peak => Math.Clamp(peak, 0, 1))],
            new Spectrogram(
                spectrogram.Frames,
                spectrogram.Bands,
                spectrogram.MinFrequency,
                spectrogram.MaxFrequency,
                spectrogram.MinDecibels,
                spectrogram.Values));
    }

    private sealed record VisualizationResponse(double DurationSeconds, double[]? Waveform, SpectrogramResponse? Spectrogram);

    // byte[] binds from the base64 string the service sends.
    private sealed record SpectrogramResponse(
        int Frames,
        int Bands,
        double MinFrequency,
        double MaxFrequency,
        double MinDecibels,
        byte[] Values);
}
