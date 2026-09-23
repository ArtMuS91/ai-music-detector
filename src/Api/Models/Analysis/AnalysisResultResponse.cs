using Core.Models;

namespace Api.Models.Analysis;

public sealed record AnalysisResultResponse(
    string Verdict,
    double AiProbability,
    double Confidence,
    IReadOnlyList<SignalResponse> Signals,
    string? Explanation)
{
    public static AnalysisResultResponse From(AnalysisResult result)
        => new(
            result.Verdict.ToString(),
            result.AiProbability,
            result.Confidence,
            [.. result.Signals.Select(s => new SignalResponse(s.Name, s.Score, s.Weight, s.Detail))],
            result.Explanation);
}
