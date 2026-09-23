using Core.Models;

namespace Core.Tests.Models;

public class AnalysisJobTests
{
    [Fact]
    public void NewJob_StartsPending_SoTheWorkerCanClaimIt()
    {
        var job = new AnalysisJob
        {
            Id = Guid.NewGuid(),
            SourceUrl = "https://music.youtube.com/watch?v=abc123",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal(AnalysisStatus.Pending, job.Status);
    }
}
