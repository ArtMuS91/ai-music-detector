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
    IAudioVisualizer visualizer,
    IEnumerable<IDetectionSignalProvider> signalProviders,
    ISignalAggregator aggregator,
    ILyricsTranscriber lyricsTranscriber,
    IResultExplainer explainer,
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

            // Saved with the move to Analyzing, so the client can show it while the detectors run.
            job.Visualization = await TryOptionalAsync(
                job, "visualize the audio", () => visualizer.VisualizeAsync(preprocessed, cancellationToken), cancellationToken);
            await AdvanceAsync(job, AnalysisStatus.Analyzing, cancellationToken);
            var signals = await CollectSignalsAsync(preprocessed, acquired.Track, cancellationToken);
            var result = aggregator.Aggregate(signals);

            // The verdict is settled above; lyrics and the AI-written explanation only word it,
            // so without them the result keeps the aggregator's own summary.
            var lyrics = await TryOptionalAsync(
                job, "transcribe lyrics", () => lyricsTranscriber.TranscribeAsync(preprocessed, cancellationToken), cancellationToken);
            var explanation = await TryOptionalAsync(
                job, "explain the result", () => explainer.ExplainAsync(acquired.Track, result, lyrics, cancellationToken), cancellationToken);

            job.Result = explanation is null ? result : result with { Explanation = explanation };
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
            job.Visualization = null;
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
            job.Visualization = null;
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
    /// For steps the result can do without (pictures, lyrics, the AI-written explanation): a
    /// failure is logged and costs only that step's output.
    /// </summary>
    private async Task<T?> TryOptionalAsync<T>(
        AnalysisJobEntity job,
        string step,
        Func<Task<T>> run,
        CancellationToken cancellationToken)
        where T : class?
    {
        try
        {
            return await run();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not {Step} for job {JobId}; continuing without it.", step, job.Id);
            return null;
        }
    }

    private async Task AdvanceAsync(AnalysisJobEntity job, AnalysisStatus status, CancellationToken cancellationToken)
    {
        job.Status = status;
        await jobs.UpdateAsync(job, cancellationToken);
    }

    private static void Fail(AnalysisJobEntity job, string reason)
    {
        job.FailedStage = job.Status switch
        {
            AnalysisStatus.Acquiring or AnalysisStatus.Preprocessing or AnalysisStatus.Analyzing => job.Status,
            // Failing again because the first failure could not be saved: it is still that stage.
            AnalysisStatus.Failed => job.FailedStage,
            // A result that could not be saved fails after Completed, which is no stage the user saw fail.
            _ => null,
        };
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
