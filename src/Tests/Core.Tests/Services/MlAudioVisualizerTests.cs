using System.Net;
using Core.Services;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Services;

public sealed class MlAudioVisualizerTests : IDisposable
{
    private const string Response = """
        {
          "duration_seconds": 180.0,
          "waveform": [0.25, 1.0, 1.4],
          "spectrogram": {
            "frames": 2, "bands": 2,
            "min_frequency": 30.0, "max_frequency": 22050.0, "min_decibels": -80.0,
            "values": "AAH//g=="
          }
        }
        """;

    private readonly string _audioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
    private readonly PreprocessedAudio _audio;

    public MlAudioVisualizerTests()
    {
        File.WriteAllBytes(_audioPath, [1, 2, 3, 4]);
        _audio = new PreprocessedAudio(_audioPath, 44_100, 1, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(42));
    }

    public void Dispose() => File.Delete(_audioPath);

    [Fact]
    public async Task Response_BecomesAVisualization_StartingWhereTheWindowStarts()
    {
        var visualizer = CreateVisualizer(new StubHandler(Response));

        var visualization = await visualizer.VisualizeAsync(_audio);

        Assert.Equal(TimeSpan.FromSeconds(42), visualization.Start);
        Assert.Equal(TimeSpan.FromMinutes(3), visualization.Duration);
        Assert.Equal([0.25, 1.0, 1.0], visualization.Waveform);
        var spectrogram = visualization.Spectrogram;
        Assert.Equal((2, 2, 30.0, 22_050.0, -80.0), (spectrogram.Frames, spectrogram.Bands, spectrogram.MinFrequency, spectrogram.MaxFrequency, spectrogram.MinDecibels));
        Assert.Equal(new byte[] { 0, 1, 255, 254 }, spectrogram.Values);
    }

    [Fact]
    public async Task UploadsTheAudioFile_ToTheVisualizeEndpoint()
    {
        var handler = new StubHandler(Response);

        await CreateVisualizer(handler).VisualizeAsync(_audio);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://ml.test/visualize", handler.Request.RequestUri!.ToString());
        Assert.Contains("name=audio", handler.Body);
    }

    [Fact]
    public async Task SpectrogramOfTheWrongSize_Throws()
    {
        var visualizer = CreateVisualizer(new StubHandler(Response.Replace("\"frames\": 2", "\"frames\": 3")));

        await Assert.ThrowsAsync<MlServiceException>(() => visualizer.VisualizeAsync(_audio));
    }

    [Fact]
    public async Task ErrorStatus_Throws()
    {
        var visualizer = CreateVisualizer(new StubHandler("""{"detail":"nope"}""", HttpStatusCode.UnprocessableEntity));

        await Assert.ThrowsAsync<MlServiceException>(() => visualizer.VisualizeAsync(_audio));
    }

    [Fact]
    public void IsRegistered()
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        Assert.IsType<MlAudioVisualizer>(services.GetRequiredService<IAudioVisualizer>());
    }

    private static MlAudioVisualizer CreateVisualizer(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://ml.test/") }, NullLogger<MlAudioVisualizer>.Instance);

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
