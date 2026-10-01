namespace Core.Services;

/// <param name="Start">Where the preprocessed window begins within the full track.</param>
public sealed record PreprocessedAudio(
    string FilePath,
    int SampleRate,
    int Channels,
    TimeSpan Duration,
    TimeSpan Start = default);

public interface IAudioPreprocessor
{
    Task<PreprocessedAudio> PreprocessAsync(AcquiredAudio audio, CancellationToken cancellationToken = default);
}
