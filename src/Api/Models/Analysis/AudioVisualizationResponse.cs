using Core.Models;

namespace Api.Models.Analysis;

public sealed record AudioVisualizationResponse(
    double StartSeconds,
    double DurationSeconds,
    IReadOnlyList<double> Waveform,
    SpectrogramResponse Spectrogram)
{
    public static AudioVisualizationResponse From(AudioVisualization visualization)
        => new(
            visualization.Start.TotalSeconds,
            visualization.Duration.TotalSeconds,
            visualization.Waveform,
            SpectrogramResponse.From(visualization.Spectrogram));
}
