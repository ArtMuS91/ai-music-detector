using Core.Models;

namespace Core.Abstractions;

public interface ISignalAggregator
{
    AnalysisResult Aggregate(IReadOnlyList<Signal> signals);
}
