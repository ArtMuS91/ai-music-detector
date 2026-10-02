using Api.Models.Analysis;
using Core.Models;
using Core.Services;
using Microsoft.Net.Http.Headers;

namespace Api.Endpoints;

public static class AnalysisEndpoints
{
    public const string SubmitRateLimitPolicy = "SubmitAnalysis";

    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analyze").WithTags("Analysis");

        group.MapPost("/", SubmitAsync)
            .WithName("SubmitAnalysis")
            .WithSummary("Queues a YouTube / YouTube Music / Spotify track for analysis, or returns its earlier completed result.")
            .RequireRateLimiting(SubmitRateLimitPolicy);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetAnalysis")
            .WithSummary("Returns the current state of a queued analysis, or 304 if it has not changed since the ETag sent in If-None-Match.");

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
                [nameof(request.Url)] = ["Not a YouTube, YouTube Music or Spotify track link."],
            });
        }

        // Already analyzed: the result is ready now, so there is nothing to accept for later.
        if (job.Status == AnalysisStatus.Completed)
        {
            return Results.Ok(AnalysisJobResponse.From(job));
        }

        return Results.AcceptedAtRoute(
            "GetAnalysis",
            new { id = job.Id },
            AnalysisJobResponse.From(job));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IAnalysisService analysis,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var job = await analysis.GetAsync(id, cancellationToken);

        if (job is null)
        {
            return Results.NotFound();
        }

        // Every write bumps UpdatedAt, so it versions the whole response. The client polls with it
        // and gets an empty 304 while the job sits in a stage, instead of the job (and its
        // visualization) again every time.
        var etag = new EntityTagHeaderValue($"\"{job.UpdatedAt.UtcTicks}\"");
        var responseHeaders = http.Response.GetTypedHeaders();
        responseHeaders.ETag = etag;
        responseHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

        var ifNoneMatch = http.Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(etag, useStrongComparison: false)))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(AnalysisJobResponse.From(job));
    }
}
