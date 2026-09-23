namespace Core.Services;

public sealed record PreprocessedAudio(
    string FilePath,
    int SampleRate,
    int Channels,
    TimeSpan Duration);

public interface IAudioPreprocessor
{
    Task<PreprocessedAudio> PreprocessAsync(AcquiredAudio audio, CancellationToken cancellationToken = default);
}
