namespace Core.Models;

/// <summary>
/// Display-sized pictures of the analyzed excerpt, kept with the job because its audio is
/// deleted once the job finishes. Not a detection signal.
/// </summary>
/// <param name="Start">Where the excerpt begins within the full track.</param>
/// <param name="Waveform">Peak amplitude per slice, 0..1, the loudest slice being 1.</param>
public sealed record AudioVisualization(
    TimeSpan Start,
    TimeSpan Duration,
    IReadOnlyList<double> Waveform,
    Spectrogram Spectrogram);

/// <param name="Values">
/// <paramref name="Frames"/> x <paramref name="Bands"/> bytes, time-major and lowest band first;
/// 0..255 maps linearly onto <paramref name="MinDecibels"/>..0 dB relative to the loudest cell.
/// Bands are log-spaced from <paramref name="MinFrequency"/> to <paramref name="MaxFrequency"/> Hz.
/// </param>
public sealed record Spectrogram(
    int Frames,
    int Bands,
    double MinFrequency,
    double MaxFrequency,
    double MinDecibels,
    byte[] Values);
