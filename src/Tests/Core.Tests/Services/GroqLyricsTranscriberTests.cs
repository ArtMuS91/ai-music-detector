using System.Net;
using System.Text.Json;
using Core.Services;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Core.Tests.Services;

public sealed class GroqLyricsTranscriberTests : IDisposable
{
    private readonly string _audioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
    private readonly PreprocessedAudio _audio;

    public GroqLyricsTranscriberTests()
    {
        File.WriteAllBytes(_audioPath, [1, 2, 3, 4]);
        _audio = new PreprocessedAudio(_audioPath, 44_100, 1, TimeSpan.FromMinutes(3));
    }

    public void Dispose() => File.Delete(_audioPath);

    [Fact]
    public async Task ConfidentSegments_BecomeLyrics_OneLinePerSegment()
    {
        var transcriber = CreateTranscriber(new StubHandler(Reply(
            Segment(" Neon rivers running through my head"),
            Segment(" I was written by a machine tonight"))));

        var lyrics = await transcriber.TranscribeAsync(_audio);

        Assert.Equal("Neon rivers running through my head\nI was written by a machine tonight", lyrics);
    }

    [Fact]
    public async Task UntrustworthySegments_AreDropped()
    {
        var transcriber = CreateTranscriber(new StubHandler(Reply(
            Segment(" Neon rivers running through my head tonight and always"),
            Segment(" Thank you for watching", noSpeech: 0.9),
            Segment(" mumble mumble", logprob: -1.6),
            Segment(" la la la la la la la la la", compression: 3.1))));

        var lyrics = await transcriber.TranscribeAsync(_audio);

        Assert.Equal("Neon rivers running through my head tonight and always", lyrics);
    }

    [Fact]
    public async Task TooFewWords_MeansNoLyrics()
    {
        var transcriber = CreateTranscriber(new StubHandler(Reply(Segment(" Oh yeah"))));

        Assert.Null(await transcriber.TranscribeAsync(_audio));
    }

    [Fact]
    public async Task UploadsTheAudio_WithModelAndVerboseFormat()
    {
        var handler = new StubHandler(Reply());

        await CreateTranscriber(handler).TranscribeAsync(_audio);

        Assert.Equal("https://api.groq.test/openai/v1/audio/transcriptions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer test-key", handler.Request.Headers.Authorization!.ToString());
        Assert.Contains("whisper-large-v3-turbo", handler.Body);
        Assert.Contains("verbose_json", handler.Body);
        Assert.Contains("\u0001\u0002\u0003\u0004", handler.Body);
    }

    [Fact]
    public async Task AudioOverTheUploadLimit_Throws_WithoutCallingGroq()
    {
        var handler = new StubHandler(Reply());
        var transcriber = CreateTranscriber(handler, new GroqOptions { ApiKey = "test-key", MaxTranscriptionUploadMegabytes = 0 });

        await Assert.ThrowsAsync<LyricsTranscriptionException>(() => transcriber.TranscribeAsync(_audio));
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task ErrorStatus_Throws()
    {
        var transcriber = CreateTranscriber(new StubHandler("""{"error":{}}""", HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<LyricsTranscriptionException>(() => transcriber.TranscribeAsync(_audio));
    }

    private static GroqLyricsTranscriber CreateTranscriber(StubHandler handler, GroqOptions? options = null)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.groq.test/openai/v1/") },
            Options.Create(options ?? new GroqOptions { ApiKey = "test-key" }),
            NullLogger<GroqLyricsTranscriber>.Instance);

    private static object Segment(string text, double noSpeech = 0.05, double logprob = -0.3, double compression = 1.4)
        => new { text, no_speech_prob = noSpeech, avg_logprob = logprob, compression_ratio = compression };

    /// <summary>Mirrors Groq's verbose_json transcription.</summary>
    private static string Reply(params object[] segments)
        => JsonSerializer.Serialize(new { text = "ignored", language = "english", segments });

    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}
