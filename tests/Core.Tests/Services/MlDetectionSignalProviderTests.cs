using System.Net;
using Core.Models;
using Core.Services;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Services;

public sealed class MlDetectionSignalProviderTests : IDisposable
{
    private static readonly Track Track = new("https://www.youtube.com/watch?v=abc123", Title: "Some Song");

    private readonly string _audioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
    private readonly PreprocessedAudio _audio;

    public MlDetectionSignalProviderTests()
    {
        File.WriteAllBytes(_audioPath, [1, 2, 3, 4]);
        _audio = new PreprocessedAudio(_audioPath, 44_100, 1, TimeSpan.FromMinutes(3));
    }

    public void Dispose() => File.Delete(_audioPath);

    [Fact]
    public async Task Response_BecomesASignal()
    {
        var provider = CreateProvider(new StubHandler(
            """{"name":"Spectral artifacts","score":0.8,"weight":0.2,"detail":"Peaks every 86 Hz."}"""));

        var signal = await provider.DetectAsync(_audio, Track);

        Assert.Equal(new Signal("Spectral artifacts", 0.8, 0.2, "Peaks every 86 Hz."), signal);
    }

    [Fact]
    public async Task UploadsTheAudioFile_ToTheDetectorsEndpoint()
    {
        var handler = new StubHandler("""{"name":"x","score":0.5,"weight":0}""");
        var provider = CreateProvider(handler, detectorId: "spectral");

        await provider.DetectAsync(_audio, Track);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://ml.test/detect/spectral", handler.Request.RequestUri!.ToString());
        Assert.Contains("name=audio", handler.Body);
        Assert.Contains("\u0001\u0002\u0003\u0004", handler.Body);
    }

    [Fact]
    public async Task OutOfRangeValues_AreClamped()
    {
        var provider = CreateProvider(new StubHandler("""{"name":"x","score":1.7,"weight":-0.3}"""));

        var signal = await provider.DetectAsync(_audio, Track);

        Assert.Equal(1, signal.Score);
        Assert.Equal(0, signal.Weight);
    }

    [Fact]
    public async Task MissingName_FallsBackToTheDetectorId()
    {
        var provider = CreateProvider(new StubHandler("""{"score":0.5,"weight":0.1}"""), detectorId: "spectral");

        var signal = await provider.DetectAsync(_audio, Track);

        Assert.Equal("spectral", signal.Name);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ErrorStatus_Throws_SoThePipelineLeavesTheSignalOut(HttpStatusCode status)
    {
        var provider = CreateProvider(new StubHandler("""{"detail":"nope"}""", status));

        await Assert.ThrowsAsync<MlServiceException>(() => provider.DetectAsync(_audio, Track));
    }

    [Fact]
    public void EachConfiguredDetector_IsRegisteredAsItsOwnProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MlService:Detectors:0"] = "spectral",
                ["MlService:Detectors:1"] = "vocals",
            })
            .Build();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

        var names = services.GetServices<IDetectionSignalProvider>().Select(p => p.Name);

        Assert.Equal(["Web research", "ML detector 'spectral'", "ML detector 'vocals'"], names);
    }

    private static MlDetectionSignalProvider CreateProvider(StubHandler handler, string detectorId = "spectral")
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("http://ml.test/") },
            detectorId,
            NullLogger<MlDetectionSignalProvider>.Instance);

    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
