using Core.Entities;

namespace Api.Models.Analysis;

public sealed record AnalysisJobResponse(
    Guid Id,
    string SourceUrl,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    TrackResponse? Track,
    AnalysisResultResponse? Result,
    AudioVisualizationResponse? Visualization,
    string? FailureReason,
    string? FailedStage)
{
    public static AnalysisJobResponse From(AnalysisJobEntity job)
        => new(
            job.Id,
            job.SourceUrl,
            job.Status.ToString(),
            job.CreatedAt,
            job.UpdatedAt,
            job.Track is null ? null : TrackResponse.From(job.Track),
            job.Result is null ? null : AnalysisResultResponse.From(job.Result),
            job.Visualization is null ? null : AudioVisualizationResponse.From(job.Visualization),
            job.FailureReason,
            job.FailedStage?.ToString());
}
