using Core.Entities;

namespace Core.Services;

/// <summary>
/// Business-logic entry point for the analysis workflow. The API layer talks to this,
/// never to <see cref="Repositories.IAnalysisJobRepository"/> directly, so URL validation
/// and any future request-time rules live in one place instead of the endpoint.
/// </summary>
public interface IAnalysisService
{
    /// <returns>The created job, or null when <paramref name="sourceUrl"/> is not a YouTube video link.</returns>
    Task<AnalysisJobEntity?> SubmitAsync(string sourceUrl, CancellationToken cancellationToken = default);

    Task<AnalysisJobEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
