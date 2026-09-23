using Core.Models;

namespace Core.Services;

public interface ISignalAggregator
{
    AnalysisResult Aggregate(IReadOnlyList<Signal> signals);
}
