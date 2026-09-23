namespace Core.Models;

public sealed class AnalysisJob
{
    public required Guid Id { get; init; }
    public required string SourceUrl { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public DateTimeOffset UpdatedAt { get; set; }
    public Track? Track { get; set; }
    public AnalysisResult? Result { get; set; }
    public string? FailureReason { get; set; }
}
