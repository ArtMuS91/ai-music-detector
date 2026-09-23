using Core.Models;

namespace Api.Models.Analysis;

public sealed record SignalResponse(
    string Name,
    double Score,
    double Weight,
    string? Detail,
    IReadOnlyList<EvidenceLinkResponse> Evidence)
{
    public static SignalResponse From(Signal signal)
        => new(
            signal.Name,
            signal.Score,
            signal.Weight,
            signal.Detail,
            [.. (signal.Evidence ?? []).Select(EvidenceLinkResponse.From)]);
}
