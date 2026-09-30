using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core.Models;
using Core.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class MlServiceException(string message) : Exception(message);

/// <summary>
/// One detector hosted by the Python <c>ml/</c> service. Each configured detector gets its own
/// provider and request, so a detector that fails or times out costs only its own signal.
/// </summary>
public sealed class MlDetectionSignalProvider(
    HttpClient http,
    string detectorId,
    ILogger<MlDetectionSignalProvider> logger) : IDetectionSignalProvider
{
    public const string HttpClientName = "MlService";

    public string Name => $"ML detector '{detectorId}'";

    public async Task<Signal> DetectAsync(PreprocessedAudio audio, Track track, CancellationToken cancellationToken = default)
    {
        await using var file = File.OpenRead(audio.FilePath);
        using var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using var form = new MultipartFormDataContent { { fileContent, "audio", Path.GetFileName(audio.FilePath) } };

        using var response = await http.PostAsync($"detect/{Uri.EscapeDataString(detectorId)}", form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("ML detector {Detector} returned {StatusCode}: {Body}", detectorId, (int)response.StatusCode, body);
            throw new MlServiceException($"ML detector '{detectorId}' failed with status {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<DetectorResponse>(cancellationToken)
            ?? throw new MlServiceException($"ML detector '{detectorId}' returned an empty response.");

        return new Signal(
            string.IsNullOrWhiteSpace(result.Name) ? detectorId : result.Name,
            Math.Clamp(result.Score, 0, 1),
            Math.Clamp(result.Weight, 0, 1),
            result.Detail);
    }

    private sealed record DetectorResponse(string? Name, double Score, double Weight, string? Detail);
}
