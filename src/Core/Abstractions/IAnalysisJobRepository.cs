using Core.Models;

namespace Core.Abstractions;

public interface IAnalysisJobRepository
{
    Task<AnalysisJob> CreateAsync(AnalysisRequest request, CancellationToken cancellationToken = default);

    Task<AnalysisJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(AnalysisJob job, CancellationToken cancellationToken = default);

    /// <summary>Claims the oldest pending job for processing, or null when the queue is empty.</summary>
    Task<AnalysisJob?> ClaimNextPendingAsync(CancellationToken cancellationToken = default);
}
