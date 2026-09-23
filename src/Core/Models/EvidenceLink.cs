namespace Core.Models;

/// <param name="Stance">Which way the source points on whether the track is AI-generated.</param>
public sealed record EvidenceLink(string Url, string? Title, EvidenceStance Stance);

public enum EvidenceStance
{
    Neutral = 0,
    Human = 1,
    AiGenerated = 2,
}
