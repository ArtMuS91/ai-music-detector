using Core.Models;

namespace Core.Tests.Models;

public class SpotifyUrlTests
{
    [Theory]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC?si=1a2b3c4d5e6f")]
    [InlineData("http://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/intl-de/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/embed/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("spotify:track:4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("  https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC  ")]
    public void TryParse_NormalizesEveryShapeToTheSameTrackId(string input)
    {
        Assert.True(SpotifyUrl.TryParse(input, out var parsed));
        Assert.Equal("4uLU6hMCjMI75M1A2tKUQC", parsed.TrackId);
        Assert.Equal("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC", parsed.CanonicalUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/album/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/playlist/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/episode/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("spotify:album:4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.notspotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("ftp://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    [InlineData("https://open.spotify.com/track/tooshort")]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKU-C")]
    [InlineData("https://open.spotify.com/de/track/4uLU6hMCjMI75M1A2tKUQC")]
    public void TryParse_RejectsAnythingThatIsNotATrackLink(string? input)
    {
        Assert.False(SpotifyUrl.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("spotify:track:4uLU6hMCjMI75M1A2tKUQC", "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC")]
    public void TrackLink_CanonicalizesBothKinds(string input, string expected)
    {
        Assert.True(TrackLink.TryGetCanonicalUrl(input, out var canonical));
        Assert.Equal(expected, canonical);
    }

    [Fact]
    public void TrackLink_RejectsOtherLinks()
    {
        Assert.False(TrackLink.TryGetCanonicalUrl("https://vimeo.com/123", out var canonical));
        Assert.Null(canonical);
    }
}
