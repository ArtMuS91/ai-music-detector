namespace Core.Models;

/// <param name="AiProbability">Aggregated 0..1 likelihood the track is AI-generated.</param>
/// <param name="Confidence">0..1 certainty in the verdict, independent of which way it leans.</param>
public sealed record AnalysisResult(
    AnalysisVerdict Verdict,
    double AiProbability,
    double Confidence,
    IReadOnlyList<Signal> Signals,
    string? Explanation = null);
