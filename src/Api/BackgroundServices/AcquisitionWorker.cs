using Core.Models;
using Core.Repositories;
using Core.Services;

namespace Api.BackgroundServices;

/// <summary>
/// Drains the pending job queue and downloads each track's audio. Phase 2 extends
/// this into the full pipeline; for now a job stops at <see cref="AnalysisStatus.Preprocessing"/>,
/// meaning "audio in hand, waiting on a stage that does not exist yet".
/// </summary>
public sealed class AcquisitionWorker(
    IServiceScopeFactory scopeFactory,
    IAudioAcquisitionService acquisition,
    ILogger<AcquisitionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await ProcessNextAsync(stoppingToken))
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Acquisition loop failed; retrying after a short delay.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IAnalysisJobRepository>();

        var job = await jobs.ClaimNextPendingAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }

        logger.LogInformation("Acquiring audio for job {JobId} ({SourceUrl}).", job.Id, job.SourceUrl);

        try
        {
            var audio = await acquisition.AcquireAsync(job.SourceUrl, cancellationToken);

            job.Track = audio.Track;
            job.AcquiredAudioPath = audio.FilePath;
            job.Status = AnalysisStatus.Preprocessing;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Acquisition failed for job {JobId}.", job.Id);
            job.Status = AnalysisStatus.Failed;
            job.FailureReason = ex.Message;
        }

        await jobs.UpdateAsync(job, cancellationToken);
        return true;
    }
}
