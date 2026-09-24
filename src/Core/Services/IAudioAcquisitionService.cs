using Core.Models;

namespace Core.Services;

public sealed record AcquiredAudio(string FilePath, Track Track);

public interface IAudioAcquisitionService
{
    Task<AcquiredAudio> AcquireAsync(string sourceUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a download returned by <see cref="AcquireAsync"/> along with anything acquisition
    /// wrote for it. Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>
    /// when it could not.
    /// </summary>
    void DeleteDownload(string filePath);
}
