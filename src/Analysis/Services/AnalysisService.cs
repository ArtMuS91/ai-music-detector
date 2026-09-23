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

        return await jobs.CreateAsync(new AnalysisRequest(url.CanonicalUrl), cancellationToken);
    }

    public Task<AnalysisJobEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => jobs.GetAsync(id, cancellationToken);
}
