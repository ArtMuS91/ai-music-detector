using Analysis.Services;
using Core.Entities;
using Core.Models;
using Core.Repositories;
using Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Services;

public sealed class AnalysisPipelineTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("pipeline-tests-").FullName;
    private readonly FakeJobRepository _jobs = new();
    private readonly FakeAcquisition _acquisition;
    private readonly FakePreprocessor _preprocessor;
    private readonly AnalysisPipeline _pipeline;

    public AnalysisPipelineTests()
    {
        _acquisition = new FakeAcquisition(_directory);
        _preprocessor = new FakePreprocessor();
        _pipeline = new AnalysisPipeline(_jobs, _acquisition, _preprocessor, NullLogger<AnalysisPipeline>.Instance);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task SuccessfulRun_CompletesInconclusively_AndReportsPreprocessingOnTheWay()
    {
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal([AnalysisStatus.Preprocessing, AnalysisStatus.Completed], _jobs.SavedStatuses);
        Assert.Equal(AnalysisVerdict.Inconclusive, job.Result?.Verdict);
        Assert.NotNull(job.Track);
    }

    [Fact]
    public async Task SuccessfulRun_DeletesBothAudioFiles()
    {
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.False(File.Exists(_acquisition.LastFilePath));
        Assert.False(File.Exists(_preprocessor.LastFilePath));
        Assert.Null(job.AcquiredAudioPath);
        Assert.Null(job.PreprocessedAudioPath);
    }

    [Fact]
    public async Task AcquisitionFailure_FailsTheJobWithTheReason()
    {
        _acquisition.Failure = new InvalidOperationException("video unavailable");
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal(AnalysisStatus.Failed, job.Status);
        Assert.Equal("video unavailable", job.FailureReason);
        Assert.Equal([AnalysisStatus.Failed], _jobs.SavedStatuses);
    }

    [Fact]
    public async Task PreprocessingFailure_FailsTheJob_AndDeletesTheDownload()
    {
        _preprocessor.Failure = new InvalidOperationException("corrupt stream");
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal(AnalysisStatus.Failed, job.Status);
        Assert.False(File.Exists(_acquisition.LastFilePath));
        Assert.Null(job.AcquiredAudioPath);
    }

    [Fact]
    public async Task Shutdown_LeavesTheJobInProgress_ForRecoveryOnNextStart()
    {
        using var shutdown = new CancellationTokenSource();
        _preprocessor.OnPreprocess = shutdown.Cancel;
        var job = NewJob(AnalysisStatus.Acquiring);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _pipeline.RunAsync(job, shutdown.Token));

        Assert.Equal(AnalysisStatus.Preprocessing, _jobs.SavedStatuses.Last());
        Assert.NotNull(job.AcquiredAudioPath);
    }

    [Fact]
    public async Task Recovery_RequeuesStrandedJobs_AndDeletesTheirAudio()
    {
        var strandedFile = Path.Combine(_directory, "stranded.webm");
        await File.WriteAllTextAsync(strandedFile, "audio");
        var stranded = NewJob(AnalysisStatus.Preprocessing);
        stranded.AcquiredAudioPath = strandedFile;
        stranded.Track = new Track(stranded.SourceUrl, Title: "Half-done");
        _jobs.InProgress.Add(stranded);

        var requeued = await _pipeline.RecoverInterruptedAsync();

        Assert.Equal(1, requeued);
        Assert.Equal(AnalysisStatus.Pending, stranded.Status);
        Assert.Null(stranded.Track);
        Assert.Null(stranded.AcquiredAudioPath);
        Assert.False(File.Exists(strandedFile));
    }

    private static AnalysisJobEntity NewJob(AnalysisStatus status) => new()
    {
        Id = Guid.NewGuid(),
        SourceUrl = "https://www.youtube.com/watch?v=abc123",
        CreatedAt = DateTimeOffset.UtcNow,
        Status = status,
    };

    private sealed class FakeJobRepository : IAnalysisJobRepository
    {
        public List<AnalysisStatus> SavedStatuses { get; } = [];
        public List<AnalysisJobEntity> InProgress { get; } = [];

        public Task UpdateAsync(AnalysisJobEntity job, CancellationToken cancellationToken = default)
        {
            SavedStatuses.Add(job.Status);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AnalysisJobEntity>> ListInProgressAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AnalysisJobEntity>>(InProgress);

        public Task<AnalysisJobEntity> CreateAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AnalysisJobEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AnalysisJobEntity?> ClaimNextPendingAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAcquisition(string directory) : IAudioAcquisitionService
    {
        public Exception? Failure { get; set; }
        public string? LastFilePath { get; private set; }

        public async Task<AcquiredAudio> AcquireAsync(string sourceUrl, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            LastFilePath = Path.Combine(directory, $"{Guid.NewGuid()}.webm");
            await File.WriteAllTextAsync(LastFilePath, "audio", cancellationToken);
            return new AcquiredAudio(LastFilePath, new Track(sourceUrl, Title: "Song"));
        }
    }

    private sealed class FakePreprocessor : IAudioPreprocessor
    {
        public Exception? Failure { get; set; }
        public Action? OnPreprocess { get; set; }
        public string? LastFilePath { get; private set; }

        public async Task<PreprocessedAudio> PreprocessAsync(AcquiredAudio audio, CancellationToken cancellationToken = default)
        {
            OnPreprocess?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            if (Failure is not null)
            {
                throw Failure;
            }

            LastFilePath = Path.ChangeExtension(audio.FilePath, ".preprocessed.wav");
            await File.WriteAllTextAsync(LastFilePath, "pcm", cancellationToken);
            return new PreprocessedAudio(LastFilePath, 44_100, 1, TimeSpan.FromMinutes(3));
        }
    }
}
