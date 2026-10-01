using Analysis.Services;
using Core.Models;

namespace Core.Tests.Services;

public class WeightedSignalAggregatorTests
{
    private readonly WeightedSignalAggregator _aggregator = new();

    [Fact]
    public void NoSignals_IsInconclusive()
    {
        var result = _aggregator.Aggregate([]);

        Assert.Equal(AnalysisVerdict.Inconclusive, result.Verdict);
        Assert.Equal(0.5, result.AiProbability);
        Assert.Equal(0, result.Confidence);
        Assert.Equal("No detection signals were available, so no verdict could be reached.", result.Explanation);
    }

    [Fact]
    public void OnlyWeightlessSignals_IsInconclusive_EvenIfTheirScoresLean()
    {
        var result = _aggregator.Aggregate([new Signal("Metadata clues", 0.9, 0), new Signal("Web research", 0.1, 0)]);

        Assert.Equal(AnalysisVerdict.Inconclusive, result.Verdict);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void OneConfidentAiSignal_IsAiGenerated()
    {
        var result = _aggregator.Aggregate([new Signal("Generator fingerprint", 0.99, 0.8)]);

        Assert.Equal(AnalysisVerdict.AiGenerated, result.Verdict);
        Assert.True(result.AiProbability > 0.95);
        Assert.True(result.Confidence > 0.9);
    }

    [Fact]
    public void SignalsThatAllSayHuman_AreConfidentlyHuman()
    {
        // The "Me at the zoo" run: every signal pointed to human.
        var result = _aggregator.Aggregate(
        [
            new Signal("Web research", 0, 0.98),
            new Signal("Generator fingerprint", 0.0001, 0.8),
            new Signal("Metadata clues", 0.05, 0.7),
        ]);

        Assert.Equal(AnalysisVerdict.Human, result.Verdict);
        Assert.Equal(0.01, result.AiProbability);
        Assert.True(result.Confidence > 0.95);
    }

    [Fact]
    public void ConfidentAudio_OutvotesLessExtremeWebResearch_AtTheSameWeight()
    {
        // The Aventhis case from Phase 3: web research called a known AI act human.
        var result = _aggregator.Aggregate(
        [
            new Signal("Generator fingerprint", 1.0, 0.8),
            new Signal("Web research", 0.05, 0.8),
            new Signal("Metadata clues", 0.5, 0),
        ]);

        Assert.Equal(AnalysisVerdict.AiGenerated, result.Verdict);
        Assert.InRange(result.AiProbability, 0.65, 0.95);
    }

    [Fact]
    public void Disagreement_LowersConfidence()
    {
        var agreed = _aggregator.Aggregate([new Signal("Generator fingerprint", 0.99, 0.8)]);
        var disputed = _aggregator.Aggregate(
        [
            new Signal("Generator fingerprint", 0.99, 0.8),
            new Signal("Web research", 0.1, 0.6),
        ]);

        Assert.Equal(AnalysisVerdict.AiGenerated, disputed.Verdict);
        Assert.True(disputed.Confidence < agreed.Confidence);
    }

    [Fact]
    public void StrongAndBalancedOnBothSides_IsMixed_WithLimitedConfidence()
    {
        // An AI cover of a well-documented human song looks like this.
        var result = _aggregator.Aggregate(
        [
            new Signal("Generator fingerprint", 0.99, 0.8),
            new Signal("Web research", 0.02, 0.98),
        ]);

        Assert.Equal(AnalysisVerdict.Mixed, result.Verdict);
        Assert.InRange(result.Confidence, 0.01, 0.6);
        Assert.StartsWith("Strong signals point both ways", result.Explanation);
    }

    [Fact]
    public void AWeakLeanAlone_IsInconclusive()
    {
        var result = _aggregator.Aggregate([new Signal("Metadata clues", 0.3, 0.2)]);

        Assert.Equal(AnalysisVerdict.Inconclusive, result.Verdict);
        Assert.True(result.Confidence < 0.1);
    }

    [Fact]
    public void ManyExtremeSignals_NeverClaimCertainty()
    {
        var result = _aggregator.Aggregate([new Signal("A", 1, 1), new Signal("B", 1, 1), new Signal("C", 1, 1)]);

        Assert.Equal(0.99, result.AiProbability);
        Assert.True(result.Confidence < 1);
    }

    [Fact]
    public void Summary_NamesTheSignalsOnEachSide_StrongestFirst()
    {
        var result = _aggregator.Aggregate(
        [
            new Signal("Metadata clues", 0.9, 0.6),
            new Signal("Generator fingerprint", 0.99, 0.8),
            new Signal("Web research", 0.2, 0.3),
            new Signal("Nothing found", 0.5, 0),
        ]);

        Assert.Equal(
            "The signals point to an AI-generated track. 3 of 4 signals had something to go on. "
            + "Pointing to AI: Generator fingerprint, Metadata clues. Pointing to human: Web research.",
            result.Explanation);
    }

    [Fact]
    public void Summary_NamesWeightedSignalsThatLeanNeitherWay()
    {
        var result = _aggregator.Aggregate(
        [
            new Signal("Generator fingerprint", 0.99, 0.8),
            new Signal("Web research", 0.5, 0.4),
        ]);

        Assert.EndsWith(
            "2 of 2 signals had something to go on. Pointing to AI: Generator fingerprint. Undecided: Web research.",
            result.Explanation);
    }

    [Fact]
    public void Signals_AreKeptAsGiven()
    {
        Signal[] signals = [new Signal("Generator fingerprint", 0.99, 0.8), new Signal("Metadata clues", 0.5, 0)];

        Assert.Equal(signals, _aggregator.Aggregate(signals).Signals);
    }
}
