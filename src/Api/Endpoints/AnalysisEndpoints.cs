using Api.Models.Analysis;
using Core.Services;

namespace Api.Endpoints;

public static class AnalysisEndpoints
{
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analyze").WithTags("Analysis");

        group.MapPost("/", SubmitAsync)
            .WithName("SubmitAnalysis")
            .WithSummary("Queues a YouTube / YouTube Music track for analysis.");

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetAnalysis")
            .WithSummary("Returns the current state of a queued analysis.");

        return app;
    }

    private static async Task<IResult> SubmitAsync(
        SubmitAnalysisRequest request,
        IAnalysisService analysis,
        CancellationToken cancellationToken)
    {
        var job = await analysis.SubmitAsync(request.Url, cancellationToken);

        if (job is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Url)] = ["Not a YouTube or YouTube Music video link."],
            });
        }

        return Results.AcceptedAtRoute(
            "GetAnalysis",
            new { id = job.Id },
            AnalysisJobResponse.From(job));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IAnalysisService analysis,
        CancellationToken cancellationToken)
    {
        var job = await analysis.GetAsync(id, cancellationToken);

        return job is null
            ? Results.NotFound()
            : Results.Ok(AnalysisJobResponse.From(job));
    }
}
