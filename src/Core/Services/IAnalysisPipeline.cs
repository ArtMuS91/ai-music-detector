using Core.Entities;

namespace Core.Services;

/// <summary>
/// Advances a claimed job through every analysis stage (acquire → preprocess → detect)
/// to a terminal status, persisting progress so polling clients see each stage.
/// </summary>
public interface IAnalysisPipeline
{
    /// <summary>
    /// Runs <paramref name="job"/> to <c>Completed</c> or <c>Failed</c> and deletes its audio files.
    /// On cancellation the job is left in its in-progress status for <see cref="RecoverInterruptedAsync"/>.
    /// </summary>
    Task RunAsync(AnalysisJobEntity job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues jobs stranded in an in-progress status by a previous shutdown and deletes
    /// their leftover audio. Only safe while no pipeline run is in flight — i.e. at startup
    /// of the single worker.
    /// </summary>
    Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken = default);
}
