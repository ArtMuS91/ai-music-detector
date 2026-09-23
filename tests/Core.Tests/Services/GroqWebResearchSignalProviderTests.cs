using System.Net;
using System.Text.Json;
using Core.Models;
using Core.Services;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Core.Tests.Services;

public class GroqWebResearchSignalProviderTests
{
    private static readonly PreprocessedAudio Audio = new("unused.wav", 44_100, 1, TimeSpan.FromMinutes(3));
    private static readonly Track Track = new("https://www.youtube.com/watch?v=abc123", Title: "Dust on the Wind", Artist: "The Velvet Sundown");

    private const string GuardianUrl = "https://www.theguardian.com/technology/2025/jul/14/ai-band";
    private const string BbcUrl = "https://bbc.com/news/articles/cp8mjnn7eqno";

    [Fact]
    public async Task Verdict_BecomesAScoredSignal_WithEvidenceFromSearchResults()
    {
        var provider = CreateProvider(GroqReply(
            content: Verdict(0.95, 0.9, (GuardianUrl, "ai"), (BbcUrl, "neutral")),
            searchResults: [(GuardianUrl, "An AI-generated band got 1m plays"), (BbcUrl, "Viral band spawns AI claims")]));

        var signal = await provider.DetectAsync(Audio, Track);

        Assert.Equal(0.95, signal.Score);
        Assert.Equal(0.9, signal.Weight);
        Assert.Equal(
            [
                new EvidenceLink(GuardianUrl, "cited title", EvidenceStance.AiGenerated),
                new EvidenceLink(BbcUrl, "cited title", EvidenceStance.Neutral),
            ],
            signal.Evidence);
    }

    [Fact]
    public async Task CitedUrlMissingFromSearchResults_IsDropped()
    {
        var provider = CreateProvider(GroqReply(
            content: Verdict(0.9, 0.9, (GuardianUrl, "ai"), ("https://made-up.example/story", "ai")),
            searchResults: [(GuardianUrl + "/", "Guardian")]));

        var signal = await provider.DetectAsync(Audio, Track);

        // Matched despite the trailing slash, and reported with the URL the search actually returned.
        Assert.Equal(GuardianUrl + "/", Assert.Single(signal.Evidence!).Url);
    }

    [Fact]
    public async Task ClaimWithNoVerifiableEvidence_CountsForLittle()
    {
        var provider = CreateProvider(GroqReply(
            content: Verdict(0.9, 0.95, ("https://made-up.example/story", "ai")),
            searchResults: []));

        var signal = await provider.DetectAsync(Audio, Track);

        Assert.Empty(signal.Evidence!);
        Assert.Equal(0.3, signal.Weight);
    }

    [Fact]
    public async Task JsonWrappedInProse_IsStillRead()
    {
        var provider = CreateProvider(GroqReply(
            content: $"Here is my answer:\n```json\n{Verdict(0.1, 0.8, (BbcUrl, "human"))}\n```",
            searchResults: [(BbcUrl, "BBC")]));

        var signal = await provider.DetectAsync(Audio, Track);

        Assert.Equal(0.1, signal.Score);
    }

    [Fact]
    public async Task ReplyWithoutJson_Throws_SoThePipelineLeavesTheSignalOut()
    {
        var provider = CreateProvider(GroqReply(content: "I could not find anything.", searchResults: []));

        await Assert.ThrowsAsync<WebResearchException>(() => provider.DetectAsync(Audio, Track));
    }

    [Fact]
    public async Task ErrorStatus_Throws()
    {
        var provider = CreateProvider("""{"error":{"message":"invalid key"}}""", HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<WebResearchException>(() => provider.DetectAsync(Audio, Track));
    }

    [Fact]
    public async Task RateLimited_RetriesAfterTheSuggestedDelay()
    {
        var handler = new StubHandler(GroqReply(Verdict(0.2, 0.7, (BbcUrl, "human")), [(BbcUrl, "BBC")]), HttpStatusCode.OK)
        {
            FailFirst = { (HttpStatusCode.TooManyRequests, "{}") },
        };
        var provider = CreateProvider(handler);

        var signal = await provider.DetectAsync(Audio, Track);

        Assert.Equal(2, handler.Calls);
        Assert.Equal(0.2, signal.Score);
    }

    [Fact]
    public async Task MalformedToolCall_IsRetried()
    {
        const string toolUseFailed = """{"error":{"message":"attempted to call tool 'JSON'","code":"tool_use_failed"}}""";
        var handler = new StubHandler(GroqReply(Verdict(0.2, 0.7, (BbcUrl, "human")), [(BbcUrl, "BBC")]), HttpStatusCode.OK)
        {
            FailFirst = { (HttpStatusCode.BadRequest, toolUseFailed) },
        };
        var provider = CreateProvider(handler);

        var signal = await provider.DetectAsync(Audio, Track);

        Assert.Equal(2, handler.Calls);
        Assert.Equal(0.2, signal.Score);
    }

    [Fact]
    public async Task RateLimitedEveryTime_GivesUp()
    {
        var handler = new StubHandler("{}", HttpStatusCode.OK)
        {
            FailFirst = { (HttpStatusCode.TooManyRequests, "{}"), (HttpStatusCode.TooManyRequests, "{}"), (HttpStatusCode.TooManyRequests, "{}") },
        };
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<WebResearchException>(() => provider.DetectAsync(Audio, Track));
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task MissingApiKey_Throws_WithoutCallingGroq()
    {
        var handler = new StubHandler("{}", HttpStatusCode.OK);
        var provider = CreateProvider(handler, apiKey: null);

        await Assert.ThrowsAsync<WebResearchException>(() => provider.DetectAsync(Audio, Track));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task TrackWithoutTitle_IsAWeightlessSignal_WithoutCallingGroq()
    {
        var handler = new StubHandler("{}", HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        var signal = await provider.DetectAsync(Audio, Track with { Title = null });

        Assert.Equal(0, signal.Weight);
        Assert.Equal(0, handler.Calls);
    }

    private static GroqWebResearchSignalProvider CreateProvider(string body, HttpStatusCode status = HttpStatusCode.OK)
        => CreateProvider(new StubHandler(body, status));

    private static GroqWebResearchSignalProvider CreateProvider(StubHandler handler, string? apiKey = "test-key")
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.test/openai/v1/") },
            Options.Create(new GroqOptions { ApiKey = apiKey }),
            NullLogger<GroqWebResearchSignalProvider>.Instance);

    private static string Verdict(double aiLikelihood, double confidence, params (string Url, string Stance)[] evidence)
        => JsonSerializer.Serialize(new
        {
            aiLikelihood,
            confidence,
            summary = "summary",
            evidence = evidence.Select(e => new { url = e.Url, title = "cited title", stance = e.Stance }),
        });

    /// <summary>Mirrors the shape of a real Groq chat completion with browser_search.</summary>
    private static string GroqReply(string content, (string Url, string Title)[] searchResults)
        => JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content,
                        executed_tools = new[]
                        {
                            new
                            {
                                name = "browser.search",
                                type = "browser_search",
                                search_results = new
                                {
                                    results = searchResults.Select(r => new { title = r.Title, url = r.Url, content = "", score = 0 }),
                                },
                            },
                        },
                    },
                },
            },
        });

    private sealed class StubHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        /// <summary>Responses returned, in order, before the stubbed one.</summary>
        public List<(HttpStatusCode Status, string Body)> FailFirst { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;

            if (Calls <= FailFirst.Count)
            {
                var (failStatus, failBody) = FailFirst[Calls - 1];
                var failure = new HttpResponseMessage(failStatus) { Content = new StringContent(failBody) };
                // Smallest delay the provider allows, to keep the test fast.
                failure.Headers.RetryAfter = new(TimeSpan.Zero);
                return Task.FromResult(failure);
            }

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
