using Core.Models;

namespace Core.Tests.Models;

public class YouTubeUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ&list=RDAMVM123")]
    [InlineData("https://www.youtube.com/watch?app=desktop&v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ")]
    public void TryParse_NormalizesEveryShapeToTheSameVideoId(string input)
    {
        Assert.True(YouTubeUrl.TryParse(input, out var parsed));
        Assert.Equal("dQw4w9WgXcQ", parsed.VideoId);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", parsed.CanonicalUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("dQw4w9WgXcQ")]
    [InlineData("ftp://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://notyoutube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=tooshort")]
    [InlineData("https://www.youtube.com/watch?v=waaaaaaaaaytoolong")]
    [InlineData("https://www.youtube.com/watch?v=has!bad@chars")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("https://www.youtube.com/feed/subscriptions")]
    public void TryParse_RejectsAnythingThatIsNotAVideoLink(string? input)
    {
        Assert.False(YouTubeUrl.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }
}
