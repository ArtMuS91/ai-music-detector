using System.Net;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>Sending to Groq with the retries every Groq integration needs.</summary>
internal static class GroqRequests
{
    /// <summary>
    /// Posts to <paramref name="path"/> and returns the response body, retrying transient failures
    /// up to <see cref="GroqOptions.MaxAttempts"/> times.
    /// </summary>
    /// <param name="createContent">Called once per attempt, since a request body can only be sent once.</param>
    /// <param name="fail">Builds the exception to throw when Groq is not configured or gives up.</param>
    public static async Task<string> PostAsync(
        HttpClient http,
        GroqOptions options,
        string path,
        Func<HttpContent> createContent,
        Func<string, Exception> fail,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw fail("Groq:ApiKey is not configured.");
        }

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = createContent() };
            request.Headers.Authorization = new("Bearer", options.ApiKey);

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return body;
            }

            if (IsTransient(response, body) && attempt < options.MaxAttempts)
            {
                var delay = RetryDelay(response);
                logger.LogInformation("Groq returned {StatusCode} for {Path}; retrying in {Delay}.", (int)response.StatusCode, path, delay);
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            logger.LogWarning("Groq returned {StatusCode} for {Path}: {Body}", (int)response.StatusCode, path, body);
            throw fail($"Groq request failed with status {(int)response.StatusCode}.");
        }
    }

    /// <summary>
    /// 429: the free tier's tokens-per-minute cap is small, and one job makes several Groq calls
    /// back to back, so it is routinely hit; the window reopens within seconds.
    /// 400 tool_use_failed: the model occasionally emits a malformed tool call; a fresh
    /// generation usually doesn't.
    /// </summary>
    private static bool IsTransient(HttpResponseMessage response, string body)
        => response.StatusCode == HttpStatusCode.TooManyRequests
            || (response.StatusCode == HttpStatusCode.BadRequest && body.Contains("\"tool_use_failed\"", StringComparison.Ordinal));

    private static TimeSpan RetryDelay(HttpResponseMessage response)
    {
        var suggested = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10);
        return TimeSpan.FromSeconds(Math.Clamp(suggested.TotalSeconds + 1, 1, 60));
    }
}
