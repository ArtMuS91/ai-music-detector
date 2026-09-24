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
    IEnumerable<IDetectionSignalProvider> signalProviders,
    ILogger<AnalysisPipeline> logger) : IAnalysisPipeline
{

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

            await AdvanceAsync(job, AnalysisStatus.Analyzing, cancellationToken);
            var signals = await CollectSignalsAsync(preprocessed, acquired.Track, cancellationToken);

            job.Result = UnaggregatedResult(signals);
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
            Fail(job, ex is UserFacingException ? ex.Message : StageFailureReason(job.Status));
        }

        DeleteAudioFiles(job);

        try
        {
            await jobs.UpdateAsync(job, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Without a terminal status the client would poll forever, so fall back to the
            // smallest possible failure record rather than leave the job in progress.
            logger.LogError(ex, "Could not save the outcome of job {JobId}; recording it as failed.", job.Id);
            job.Result = null;
            Fail(job, "The analysis finished, but its result could not be saved.");
            await jobs.UpdateAsync(job, cancellationToken);
        }
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

    /// <summary>
    /// Runs every provider; one that throws is logged and left out, so a single broken or
    /// unconfigured detector costs that signal rather than the whole analysis.
    /// </summary>
    private async Task<List<Signal>> CollectSignalsAsync(
        PreprocessedAudio audio,
        Track track,
        CancellationToken cancellationToken)
    {
        var signals = new List<Signal>();

        foreach (var provider in signalProviders)
        {
            try
            {
                signals.Add(await provider.DetectAsync(audio, track, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Signal provider {Provider} failed; continuing without it.", provider.Name);
            }
        }

        return signals;
    }

    /// <summary>
    /// Stand-in until aggregation (Phase 4) exists: signals are kept for display, but the
    /// verdict stays inconclusive rather than guessing how to weigh them.
    /// </summary>
    private static AnalysisResult UnaggregatedResult(IReadOnlyList<Signal> signals) => new(
        AnalysisVerdict.Inconclusive,
        AiProbability: 0.5,
        Confidence: 0,
        signals,
        Explanation: signals.Count == 0
            ? "No detection signals are available, so no verdict could be reached."
            : "Signals were collected, but combining them into a verdict is not implemented yet.");

    private async Task AdvanceAsync(AnalysisJobEntity job, AnalysisStatus status, CancellationToken cancellationToken)
    {
        job.Status = status;
        await jobs.UpdateAsync(job, cancellationToken);
    }

    private static void Fail(AnalysisJobEntity job, string reason)
    {
        job.Status = AnalysisStatus.Failed;
        job.FailureReason = reason.Length <= AnalysisJobEntity.FailureReasonMaxLength
            ? reason
            : reason[..AnalysisJobEntity.FailureReasonMaxLength];
    }

    /// <summary>What the user sees for an unexpected failure; the details only go to the log.</summary>
    private static string StageFailureReason(AnalysisStatus stage) => stage switch
    {
        AnalysisStatus.Acquiring =>
            "Could not download the track's audio. The video may be private, age-restricted, region-locked or no longer available.",
        AnalysisStatus.Preprocessing => "Could not decode the track's audio.",
        _ => "The analysis failed unexpectedly.",
    };

    /// <summary>Audio is only needed while a job is being worked on; nothing reads it after that.</summary>
    private void DeleteAudioFiles(AnalysisJobEntity job)
    {
        // Preprocessed output first: it can live inside the download folder DeleteDownload removes.
        job.PreprocessedAudioPath = TryDelete(job.PreprocessedAudioPath, File.Delete);
        job.AcquiredAudioPath = TryDelete(job.AcquiredAudioPath, acquisition.DeleteDownload);
    }

    /// <returns>Null once the file is gone, or the path again if it could not be deleted.</returns>
    private string? TryDelete(string? path, Action<string> delete)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            delete(path);
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            // Its folder was already removed along with it.
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete audio file {Path}.", path);
            return path;
        }
    }
}
