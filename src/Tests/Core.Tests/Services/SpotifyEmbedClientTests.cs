using System.Net;
using System.Text.Json;
using Core.Models;
using Core.Services;
using Infrastructure.Services;

namespace Core.Tests.Services;

/// <summary>
/// Parses a real embed page (Fixtures/spotify-embed.html, trimmed to the track entity), so a
/// change in Spotify's undocumented page shape shows up here.
/// </summary>
public class SpotifyEmbedClientTests
{
    private static readonly SpotifyUrl Url = SpotifyUrl.TryParse("spotify:track:4uLU6hMCjMI75M1A2tKUQC", out var url)
        ? url
        : throw new InvalidOperationException();

    [Fact]
    public void RealEmbedPage_MapsEveryTrackField()
    {
        var track = SpotifyEmbedClient.ParseEmbedPage(Fixture());

        Assert.NotNull(track);
        Assert.Equal("Never Gonna Give You Up", track.Title);
        Assert.Equal(["Rick Astley"], track.Artists);
        Assert.Equal(TimeSpan.FromMilliseconds(213_573), track.Duration);
        Assert.Equal(new DateTimeOffset(1987, 11, 12, 0, 0, 0, TimeSpan.Zero), track.ReleasedAt);
    }

    [Fact]
    public void NotFoundPage_HasNoTrack()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"status":404,"title":"Page not found"}}}</script>
            """;

        Assert.Null(SpotifyEmbedClient.ParseEmbedPage(html));
    }

    [Fact]
    public void PageWithoutData_Throws()
    {
        Assert.Throws<JsonException>(() => SpotifyEmbedClient.ParseEmbedPage("<html></html>"));
    }

    [Fact]
    public async Task GetTrack_RequestsTheEmbedPage()
    {
        var handler = new StubHandler(Fixture());

        var track = await CreateClient(handler).GetTrackAsync(Url);

        Assert.Equal("http://spotify.test/embed/track/4uLU6hMCjMI75M1A2tKUQC", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Never Gonna Give You Up", track.Title);
    }

    [Fact]
    public async Task GetTrack_UnknownTrack_IsAUserFacingFailure()
    {
        var handler = new StubHandler("""<script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"status":404}}}</script>""");

        await Assert.ThrowsAsync<UserFacingException>(() => CreateClient(handler).GetTrackAsync(Url));
    }

    [Fact]
    public async Task GetTrack_ErrorStatus_Throws()
    {
        var handler = new StubHandler("", HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).GetTrackAsync(Url));
    }

    private static string Fixture()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spotify-embed.html"));

    private static SpotifyEmbedClient CreateClient(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://spotify.test/") });

    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
