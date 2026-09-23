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
    string? FailureReason)
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
            job.FailureReason);
}
