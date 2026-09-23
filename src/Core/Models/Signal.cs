namespace Core.Models;

/// <param name="Score">0 = strongly human, 1 = strongly AI-generated.</param>
/// <param name="Weight">Relative influence on the aggregate verdict.</param>
public sealed record Signal(
    string Name,
    double Score,
    double Weight,
    string? Detail = null);
