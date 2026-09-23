namespace Core.Models;

/// <param name="Score">0 = strongly human, 1 = strongly AI-generated.</param>
/// <param name="Weight">Relative influence on the aggregate verdict. 0 means the signal found nothing to go on.</param>
/// <param name="Evidence">Sources backing the signal, for detectors that work from outside information.</param>
public sealed record Signal(
    string Name,
    double Score,
    double Weight,
    string? Detail = null,
    IReadOnlyList<EvidenceLink>? Evidence = null);
