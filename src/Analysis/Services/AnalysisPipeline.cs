using Core.Entities;
using Core.Models;
using Core.Repositories;
using Core.Services;
using Microsoft.Extensions.Logging;

namespace Analysis.Services;

public sealed class AnalysisPipeline(
    IAnalysisJobRepository jobs,
    IAudioAcquisitionService acquisition,
    IAudioPreprocessor preprocessor,
    ILogger<AnalysisPipeline> logger) : IAnalysisPipeline
{
    /// <summary>
    /// Stand-in until detection signals (Phase 3) and aggregation (Phase 4) exist: with no
    /// signals the only honest verdict is inconclusive, so jobs still reach a terminal state.
    /// </summary>
    private static readonly AnalysisResult NoSignalsResult = new(
        AnalysisVerdict.Inconclusive,
        AiProbability: 0.5,
        Confidence: 0,
        Signals: [],
        Explanation: "No detection signals are available yet, so no verdict could be reached.");

    public async Task RunAsync(AnalysisJobEntity job, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Acquiring audio for job {JobId} ({SourceUrl}).", job.Id, job.SourceUrl);
            var acquired = await acquisition.AcquireAsync(job.SourceUrl, cancellationToken);

            job.Track = acquired.Track;
            job.AcquiredAudioPath = acquired.FilePath;
            await AdvanceAsync(job, AnalysisStatus.Preprocessing, cancellationToken);

            var preprocessed = await preprocessor.PreprocessAsync(acquired, cancellationToken);

            job.PreprocessedAudioPath = preprocessed.FilePath;
            logger.LogInformation(
                "Preprocessed job {JobId}: {Duration} at {SampleRate} Hz, {Channels} channel(s).",
                job.Id,
                preprocessed.Duration,
                preprocessed.SampleRate,
                preprocessed.Channels);

            job.Result = NoSignalsResult;
            job.Status = AnalysisStatus.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown: leave the job in-progress so RecoverInterruptedAsync requeues it on next start.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Job {JobId} failed during {Status}.", job.Id, job.Status);
            job.Status = AnalysisStatus.Failed;
            job.FailureReason = ex.Message;
        }

        DeleteAudioFiles(job);
        await jobs.UpdateAsync(job, cancellationToken);
    }

    public async Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken = default)
    {
        var interrupted = await jobs.ListInProgressAsync(cancellationToken);

        foreach (var job in interrupted)
        {
            logger.LogWarning("Requeuing job {JobId}, interrupted during {Status}.", job.Id, job.Status);

            DeleteAudioFiles(job);
            job.Track = null;
            job.Status = AnalysisStatus.Pending;
            await jobs.UpdateAsync(job, cancellationToken);
        }

        return interrupted.Count;
    }

    private async Task AdvanceAsync(AnalysisJobEntity job, AnalysisStatus status, CancellationToken cancellationToken)
    {
        job.Status = status;
        await jobs.UpdateAsync(job, cancellationToken);
    }

    /// <summary>Audio is only needed while a job is being worked on; nothing reads it after that.</summary>
    private void DeleteAudioFiles(AnalysisJobEntity job)
    {
        job.AcquiredAudioPath = TryDelete(job.AcquiredAudioPath);
        job.PreprocessedAudioPath = TryDelete(job.PreprocessedAudioPath);
    }

    /// <returns>Null once the file is gone, or the path again if it could not be deleted.</returns>
    private string? TryDelete(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            File.Delete(path);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete audio file {Path}.", path);
            return path;
        }
    }
}
