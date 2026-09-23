namespace Api.Models.Analysis;

public sealed record SignalResponse(string Name, double Score, double Weight, string? Detail);
