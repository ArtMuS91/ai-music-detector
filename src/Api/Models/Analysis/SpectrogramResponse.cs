using Core.Models;

namespace Api.Models.Analysis;

/// <param name="Values">
/// Base64 of <paramref name="Frames"/> x <paramref name="Bands"/> bytes, time-major and lowest band
/// first; 0..255 maps onto <paramref name="MinDecibels"/>..0 dB. Bands are log-spaced in frequency.
/// </param>
public sealed record SpectrogramResponse(
    int Frames,
    int Bands,
    double MinFrequency,
    double MaxFrequency,
    double MinDecibels,
    string Values)
{
    public static SpectrogramResponse From(Spectrogram spectrogram)
        => new(
            spectrogram.Frames,
            spectrogram.Bands,
            spectrogram.MinFrequency,
            spectrogram.MaxFrequency,
            spectrogram.MinDecibels,
            Convert.ToBase64String(spectrogram.Values));
}
