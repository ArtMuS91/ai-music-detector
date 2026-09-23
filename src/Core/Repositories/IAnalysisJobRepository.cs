using Core.Entities;
using Core.Models;

namespace Core.Repositories;

public interface IAnalysisJobRepository
{
    Task<AnalysisJobEntity> CreateAsync(AnalysisRequest request, CancellationToken cancellationToken = default);

    Task<AnalysisJobEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(AnalysisJobEntity job, CancellationToken cancellationToken = default);

    /// <summary>Claims the oldest pending job for processing, or null when the queue is empty.</summary>
    Task<AnalysisJobEntity?> ClaimNextPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Jobs that were claimed but have not reached <c>Completed</c> or <c>Failed</c>.</summary>
    Task<IReadOnlyList<AnalysisJobEntity>> ListInProgressAsync(CancellationToken cancellationToken = default);
}
