using Core.Entities;
using Core.Models;
using Core.Repositories;
using Core.Services;

namespace Analysis.Services;

public sealed class AnalysisService(IAnalysisJobRepository jobs) : IAnalysisService
{
    public async Task<AnalysisJobEntity?> SubmitAsync(string sourceUrl, CancellationToken cancellationToken = default)
    {
        if (!YouTubeUrl.TryParse(sourceUrl, out var url))
        {
            return null;
        }

        // A finished analysis of the same video is returned as is: re-running it would cost a full
        // download and several Groq calls to reach the same answer. Failed jobs are not reused,
        // so a link that failed (a video that was private for a while, say) can be retried.
        return await jobs.FindLatestCompletedAsync(url.CanonicalUrl, cancellationToken)
            ?? await jobs.CreateAsync(new AnalysisRequest(url.CanonicalUrl), cancellationToken);
    }

    public Task<AnalysisJobEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => jobs.GetAsync(id, cancellationToken);
}
