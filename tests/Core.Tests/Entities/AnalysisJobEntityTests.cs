using Core.Entities;
using Core.Models;

namespace Core.Tests.Entities;

public class AnalysisJobEntityTests
{
    [Fact]
    public void NewJob_StartsPending_SoTheWorkerCanClaimIt()
    {
        var job = new AnalysisJobEntity
        {
            Id = Guid.NewGuid(),
            SourceUrl = "https://music.youtube.com/watch?v=abc123",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal(AnalysisStatus.Pending, job.Status);
    }
}
