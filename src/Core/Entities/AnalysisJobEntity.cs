using Core.Models;

namespace Core.Entities;

public sealed class AnalysisJobEntity
{
    public const int FailureReasonMaxLength = 2048;

    public required Guid Id { get; init; }
    public required string SourceUrl { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public DateTimeOffset UpdatedAt { get; set; }
    public Track? Track { get; set; }
    public string? AcquiredAudioPath { get; set; }
    public string? PreprocessedAudioPath { get; set; }
    public AnalysisResult? Result { get; set; }
    public string? FailureReason { get; set; }
}
