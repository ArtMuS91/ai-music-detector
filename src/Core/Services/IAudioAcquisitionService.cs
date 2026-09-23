using Core.Models;

namespace Core.Services;

public sealed record AcquiredAudio(string FilePath, Track Track);

public interface IAudioAcquisitionService
{
    Task<AcquiredAudio> AcquireAsync(string sourceUrl, CancellationToken cancellationToken = default);
}
