using Analysis.Services;
using Core.Entities;
using Core.Models;
using Core.Repositories;

namespace Core.Tests.Services;

public class AnalysisServiceTests
{
    private const string Canonical = "https://www.youtube.com/watch?v=jNQXAC9IVRw";

    private readonly FakeJobRepository _jobs = new();
    private readonly AnalysisService _service;

    public AnalysisServiceTests() => _service = new AnalysisService(_jobs);

    [Fact]
    public async Task NewLink_QueuesAJob_UnderItsCanonicalUrl()
    {
        var job = await _service.SubmitAsync("https://music.youtube.com/watch?v=jNQXAC9IVRw&list=RDAMVM");

        Assert.Equal(AnalysisStatus.Pending, job!.Status);
        Assert.Equal(Canonical, Assert.Single(_jobs.Created).SourceUrl);
    }

    [Fact]
    public async Task AlreadyAnalyzedVideo_ReturnsTheEarlierResult_WithoutQueuingAgain()
    {
        var earlier = Job(AnalysisStatus.Completed);
        _jobs.Completed[Canonical] = earlier;

        // A different URL shape for the same video still finds it.
        var job = await _service.SubmitAsync("https://youtu.be/jNQXAC9IVRw?si=share");

        Assert.Same(earlier, job);
        Assert.Empty(_jobs.Created);
    }

    [Fact]
    public async Task SpotifyLink_IsQueuedUnderItsCanonicalUrl()
    {
        await _service.SubmitAsync("https://open.spotify.com/intl-de/track/4uLU6hMCjMI75M1A2tKUQC?si=share");

        var request = Assert.Single(_jobs.Created);
        Assert.Equal("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC", request.SourceUrl);
    }

    [Fact]
    public async Task InvalidLink_IsRejected_WithoutTouchingTheStore()
    {
        Assert.Null(await _service.SubmitAsync("https://vimeo.com/123"));
        Assert.Empty(_jobs.Created);
        Assert.Equal(0, _jobs.Lookups);
    }

    private static AnalysisJobEntity Job(AnalysisStatus status) => new()
    {
        Id = Guid.NewGuid(),
        SourceUrl = Canonical,
        CreatedAt = DateTimeOffset.UtcNow,
        Status = status,
    };

    /// <summary>Only completed jobs are registered here, mirroring the repository query's filter.</summary>
    private sealed class FakeJobRepository : IAnalysisJobRepository
    {
        public Dictionary<string, AnalysisJobEntity> Completed { get; } = [];
        public List<AnalysisRequest> Created { get; } = [];
        public int Lookups { get; private set; }

        public Task<AnalysisJobEntity?> FindLatestCompletedAsync(string sourceUrl, CancellationToken cancellationToken = default)
        {
            Lookups++;
            return Task.FromResult(Completed.GetValueOrDefault(sourceUrl));
        }

        public Task<AnalysisJobEntity> CreateAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
        {
            Created.Add(request);
            return Task.FromResult(new AnalysisJobEntity
            {
                Id = Guid.NewGuid(),
                SourceUrl = request.SourceUrl,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        public Task<AnalysisJobEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(AnalysisJobEntity job, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AnalysisJobEntity?> ClaimNextPendingAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AnalysisJobEntity>> ListInProgressAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
