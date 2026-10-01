using Core.Models;
using Core.Services;

namespace Analysis.Services;

/// <summary>
/// Combines independent signals by weighted log-odds pooling: each signal contributes
/// <c>weight × logit(score)</c> evidence units (positive toward AI, negative toward human), and the
/// sum, passed back through the logistic function, is the AI probability. A confident score
/// (1.00 from the audio fingerprint) therefore outweighs a less extreme one at the same weight,
/// which is how strong audio evidence can outvote web research that got a track wrong.
/// <para>
/// Hand-set rules, not a learned meta-model: there is no labelled set of results to fit one on yet.
/// Signals are treated as independent, which they are not quite (web research and metadata can
/// read the same clues), so probabilities near 0 or 1 overstate certainty somewhat.
/// </para>
/// </summary>
public sealed class WeightedSignalAggregator : ISignalAggregator
{
    /// <summary>
    /// Scores are pulled this far in from 0 and 1, so no single signal is infinite evidence, and the
    /// combined probability stops there too: signals that are not truly independent never add up
    /// to certainty.
    /// </summary>
    private const double ScoreClamp = 0.01;

    /// <summary>Evidence on one side at least this strong (two confident signals, roughly) counts as a strong case.</summary>
    private const double StrongSide = 2.0;

    /// <summary>
    /// With strong cases on both sides, the winner needs this much net evidence (about 73%) to take
    /// the verdict; below it the track is called Mixed.
    /// </summary>
    private const double Dominance = 1.0;

    /// <summary>A verdict either way needs the probability past 65% / below 35%.</summary>
    private const double DecisiveProbability = 0.65;

    /// <summary>Conflicting signals are weak proof that a track is truly mixed, so that verdict never claims more.</summary>
    private const double MaxMixedConfidence = 0.6;

    public AnalysisResult Aggregate(IReadOnlyList<Signal> signals)
    {
        var contributions = signals
            .Where(s => s.Weight > 0)
            .Select(s => (Signal: s, Evidence: s.Weight * Logit(Math.Clamp(s.Score, ScoreClamp, 1 - ScoreClamp))))
            .ToList();

        if (contributions.Count == 0)
        {
            return new AnalysisResult(
                AnalysisVerdict.Inconclusive,
                AiProbability: 0.5,
                Confidence: 0,
                signals,
                signals.Count == 0
                    ? "No detection signals were available, so no verdict could be reached."
                    : "None of the signals found anything to go on, so no verdict could be reached.");
        }

        var towardAi = contributions.Where(c => c.Evidence > 0).Sum(c => c.Evidence);
        var towardHuman = -contributions.Where(c => c.Evidence < 0).Sum(c => c.Evidence);
        var net = towardAi - towardHuman;
        var aiProbability = Math.Clamp(Logistic(net), ScoreClamp, 1 - ScoreClamp);

        // 1 when every signal points the same way, 0 when they cancel out exactly.
        var agreement = towardAi + towardHuman > 0 ? Math.Abs(net) / (towardAi + towardHuman) : 0;

        AnalysisVerdict verdict;
        double confidence;

        if (towardAi >= StrongSide && towardHuman >= StrongSide && Math.Abs(net) < Dominance)
        {
            verdict = AnalysisVerdict.Mixed;
            confidence = Math.Min(1 - agreement, MaxMixedConfidence);
        }
        else
        {
            verdict = aiProbability >= DecisiveProbability ? AnalysisVerdict.AiGenerated
                : aiProbability <= 1 - DecisiveProbability ? AnalysisVerdict.Human
                : AnalysisVerdict.Inconclusive;
            // How far the probability is from a coin flip, discounted when signals disagree.
            confidence = Math.Abs(2 * aiProbability - 1) * (0.5 + 0.5 * agreement);
        }

        return new AnalysisResult(
            verdict,
            Math.Round(aiProbability, 4),
            Math.Round(confidence, 4),
            signals,
            Summarize(verdict, contributions, signals.Count));
    }

    /// <summary>Rule-based explanation, kept when the AI-written one is unavailable.</summary>
    private static string Summarize(
        AnalysisVerdict verdict,
        List<(Signal Signal, double Evidence)> contributions,
        int signalCount)
    {
        var headline = verdict switch
        {
            AnalysisVerdict.AiGenerated => "The signals point to an AI-generated track.",
            AnalysisVerdict.Human => "The signals point to a human-made track.",
            AnalysisVerdict.Mixed =>
                "Strong signals point both ways: the track may combine human and AI work (an AI cover of a human song, say), or one of the detectors is wrong.",
            _ => "The signals are too weak or too evenly balanced to call.",
        };

        var parts = new List<string>
        {
            headline,
            $"{contributions.Count} of {signalCount} signals had something to go on.",
        };

        if (Strongest(contributions, ai: true) is { } ai)
        {
            parts.Add($"Pointing to AI: {ai}.");
        }

        if (Strongest(contributions, ai: false) is { } human)
        {
            parts.Add($"Pointing to human: {human}.");
        }

        // Weighted but scored exactly 0.5: counted above as having something to go on, so name them too.
        var undecided = contributions.Where(c => c.Evidence == 0).Select(c => c.Signal.Name).ToList();
        if (undecided.Count > 0)
        {
            parts.Add($"Undecided: {string.Join(", ", undecided)}.");
        }

        return string.Join(" ", parts);
    }

    private static string? Strongest(List<(Signal Signal, double Evidence)> contributions, bool ai)
    {
        var names = contributions
            .Where(c => ai ? c.Evidence > 0 : c.Evidence < 0)
            .OrderByDescending(c => Math.Abs(c.Evidence))
            .Select(c => c.Signal.Name)
            .ToList();

        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static double Logit(double p) => Math.Log(p / (1 - p));

    private static double Logistic(double x) => 1 / (1 + Math.Exp(-x));
}
