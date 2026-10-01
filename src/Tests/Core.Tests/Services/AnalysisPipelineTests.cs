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
    private readonly FakeVisualizer _visualizer = new();
    private readonly List<IDetectionSignalProvider> _signalProviders = [];
    private readonly AnalysisPipeline _pipeline;

    public AnalysisPipelineTests()
    {
        _acquisition = new FakeAcquisition(_directory);
        _preprocessor = new FakePreprocessor();
        _pipeline = new AnalysisPipeline(_jobs, _acquisition, _preprocessor, _visualizer, _signalProviders, NullLogger<AnalysisPipeline>.Instance);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task SuccessfulRun_CompletesInconclusively_AndReportsEachStageOnTheWay()
    {
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal(
            [AnalysisStatus.Preprocessing, AnalysisStatus.Analyzing, AnalysisStatus.Completed],
            _jobs.SavedStatuses);
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
    public async Task Signals_AreKeptInTheResult_WithTheirEvidence()
    {
        var evidence = new EvidenceLink("https://example.com/ai-band", "AI band exposed", EvidenceStance.AiGenerated);
        _signalProviders.Add(new FakeSignalProvider(new Signal("Web research", 0.9, 0.8, "Known AI act", [evidence])));
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        var signal = Assert.Single(job.Result!.Signals);
        Assert.Equal([evidence], signal.Evidence);
        Assert.Equal(AnalysisVerdict.Inconclusive, job.Result.Verdict);
    }

    [Fact]
    public async Task FailingSignalProvider_IsLeftOut_WithoutFailingTheJob()
    {
        _signalProviders.Add(new FakeSignalProvider(failure: new HttpRequestException("Groq is down")));
        _signalProviders.Add(new FakeSignalProvider(new Signal("Spectral", 0.2, 0.5)));
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal(AnalysisStatus.Completed, job.Status);
        Assert.Equal("Spectral", Assert.Single(job.Result!.Signals).Name);
    }

    [Fact]
    public async Task Visualization_IsSavedWithTheMoveToAnalyzing_SoTheClientCanShowItEarly()
    {
        var job = NewJob(AnalysisStatus.Acquiring);
        AudioVisualization? savedWhileAnalyzing = null;
        _jobs.OnUpdate = saved =>
        {
            if (saved.Status == AnalysisStatus.Analyzing)
            {
                savedWhileAnalyzing = saved.Visualization;
            }
        };

        await _pipeline.RunAsync(job);

        Assert.Same(_visualizer.Visualization, savedWhileAnalyzing);
        Assert.Same(_visualizer.Visualization, job.Visualization);
        Assert.Equal(TimeSpan.FromSeconds(45), _visualizer.LastAudio!.Start);
    }

    [Fact]
    public async Task FailingVisualizer_IsLeftOut_WithoutFailingTheJob()
    {
        _visualizer.Failure = new HttpRequestException("ML service is down");
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal(AnalysisStatus.Completed, job.Status);
        Assert.Null(job.Visualization);
    }

    [Fact]
    public async Task AcquisitionFailure_FailsTheJobWithTheReason()
    {
        _acquisition.Failure = new UserFacingException("video unavailable");
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal(AnalysisStatus.Failed, job.Status);
        Assert.Equal("video unavailable", job.FailureReason);
        Assert.Equal(AnalysisStatus.Acquiring, job.FailedStage);
        Assert.Equal([AnalysisStatus.Failed], _jobs.SavedStatuses);
    }

    [Fact]
    public async Task PreprocessingFailure_FailsTheJob_AndDeletesTheDownload()
    {
        _preprocessor.Failure = new InvalidOperationException("corrupt stream");
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal(AnalysisStatus.Failed, job.Status);
        Assert.Equal(AnalysisStatus.Preprocessing, job.FailedStage);
        Assert.False(File.Exists(_acquisition.LastFilePath));
        Assert.Null(job.AcquiredAudioPath);
    }

    [Fact]
    public async Task UnsavableResult_FailsTheJob_WithoutClaimingAStageFailed()
    {
        _jobs.OnUpdate = saved =>
        {
            if (saved.Status == AnalysisStatus.Completed)
            {
                throw new InvalidOperationException("row too large");
            }
        };
        var job = NewJob(AnalysisStatus.Acquiring);

        await _pipeline.RunAsync(job);

        Assert.Equal(AnalysisStatus.Failed, job.Status);
        Assert.Null(job.FailedStage);
        Assert.Null(job.Result);
        Assert.Null(job.Visualization);
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
        stranded.Visualization = FakeVisualizer.Sample;
        _jobs.InProgress.Add(stranded);

        var requeued = await _pipeline.RecoverInterruptedAsync();

        Assert.Equal(1, requeued);
        Assert.Equal(AnalysisStatus.Pending, stranded.Status);
        Assert.Null(stranded.Track);
        Assert.Null(stranded.Visualization);
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
        public Action<AnalysisJobEntity>? OnUpdate { get; set; }

        public Task UpdateAsync(AnalysisJobEntity job, CancellationToken cancellationToken = default)
        {
            SavedStatuses.Add(job.Status);
            OnUpdate?.Invoke(job);
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

    private sealed class FakeSignalProvider(Signal? signal = null, Exception? failure = null) : IDetectionSignalProvider
    {
        public string Name => signal?.Name ?? "Failing";

        public Task<Signal> DetectAsync(PreprocessedAudio audio, Track track, CancellationToken cancellationToken = default)
            => failure is null ? Task.FromResult(signal!) : Task.FromException<Signal>(failure);
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

        public void DeleteDownload(string filePath) => File.Delete(filePath);
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
            return new PreprocessedAudio(LastFilePath, 44_100, 1, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(45));
        }
    }

    private sealed class FakeVisualizer : IAudioVisualizer
    {
        public static readonly AudioVisualization Sample = new(
            TimeSpan.FromSeconds(45),
            TimeSpan.FromMinutes(3),
            [0.5, 1],
            new Spectrogram(1, 2, 30, 22_050, -80, [0, 255]));

        public AudioVisualization Visualization { get; } = Sample;
        public Exception? Failure { get; set; }
        public PreprocessedAudio? LastAudio { get; private set; }

        public Task<AudioVisualization> VisualizeAsync(PreprocessedAudio audio, CancellationToken cancellationToken = default)
        {
            LastAudio = audio;
            return Failure is null ? Task.FromResult(Visualization) : Task.FromException<AudioVisualization>(Failure);
        }
    }
}
