using Core.Repositories;
using Core.Services;

namespace Api.BackgroundServices;

/// <summary>
/// The single consumer of the job queue: claims one pending job at a time and hands it
/// to <see cref="IAnalysisPipeline"/>, which runs it to a terminal status.
/// </summary>
/// <remarks>
/// Being the only worker is what makes startup recovery safe — when this service starts,
/// no job can genuinely be in flight, so anything still marked in-progress was stranded by
/// the previous shutdown. Running a second worker (another replica, or parallel loops here)
/// would need a lease/heartbeat instead, or it would requeue a job another worker still owns.
/// </remarks>
public sealed class AnalysisWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AnalysisWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedAsync(stoppingToken);

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
                logger.LogError(ex, "Analysis loop failed; retrying after a short delay.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private async Task RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IAnalysisPipeline>();

        var requeued = await pipeline.RecoverInterruptedAsync(cancellationToken);
        if (requeued > 0)
        {
            logger.LogInformation("Requeued {Count} job(s) interrupted by the previous shutdown.", requeued);
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

        await scope.ServiceProvider.GetRequiredService<IAnalysisPipeline>().RunAsync(job, cancellationToken);
        return true;
    }
}
