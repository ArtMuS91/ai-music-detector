using Core.Models;

namespace Core.Services;

/// <summary>
/// One independent detection signal. Providers run side by side so a weak or
/// failing detector degrades the verdict's confidence instead of the whole analysis.
/// </summary>
public interface IDetectionSignalProvider
{
    string Name { get; }

    Task<Signal> DetectAsync(PreprocessedAudio audio, Track track, CancellationToken cancellationToken = default);
}
