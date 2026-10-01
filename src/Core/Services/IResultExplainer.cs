using Core.Models;

namespace Core.Services;

/// <summary>
/// Writes the human-readable explanation of an aggregated result. It explains the verdict the
/// aggregator reached; it never changes the verdict, probability or confidence.
/// </summary>
public interface IResultExplainer
{
    /// <param name="lyrics">Transcribed lyrics, if any, as extra context.</param>
    Task<string> ExplainAsync(Track track, AnalysisResult result, string? lyrics, CancellationToken cancellationToken = default);
}
