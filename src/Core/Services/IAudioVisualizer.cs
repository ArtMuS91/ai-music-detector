using Core.Models;

namespace Core.Services;

/// <summary>Renders the preprocessed audio into the waveform and spectrogram shown with the result.</summary>
public interface IAudioVisualizer
{
    Task<AudioVisualization> VisualizeAsync(PreprocessedAudio audio, CancellationToken cancellationToken = default);
}
