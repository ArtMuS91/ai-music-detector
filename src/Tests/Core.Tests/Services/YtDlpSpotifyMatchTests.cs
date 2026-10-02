using System.Text.Json;
using Core.Models;
using Infrastructure.Services;
using static Infrastructure.Services.YtDlpAudioAcquisitionService;

namespace Core.Tests.Services;

/// <summary>
/// How a Spotify track is matched to a YouTube video: mapping a real yt-dlp search
/// (Fixtures/yt-dlp-search.json), picking a result and merging the two tracks' metadata.
/// </summary>
public class YtDlpSpotifyMatchTests
{
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(15);

    private static readonly SpotifyTrack Spotify = new(
        "Never Gonna Give You Up",
        ["Rick Astley"],
        TimeSpan.FromMilliseconds(213_573),
        new DateTimeOffset(1987, 11, 12, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void RealSearch_MapsEveryVideo()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "yt-dlp-search.json")));

        var candidates = SearchCandidatesFrom(document.RootElement);

        Assert.Equal(5, candidates.Count);
        Assert.Equal("dQw4w9WgXcQ", candidates[0].Url.VideoId);
        Assert.Equal(TimeSpan.FromSeconds(214), candidates[0].Duration);
        Assert.All(candidates, candidate => Assert.False(candidate.IsLive));
    }

    [Fact]
    public void Search_SkipsEntriesThatAreNotVideos()
    {
        using var document = JsonDocument.Parse("""
            {"entries": [
              {"url": "https://www.youtube.com/channel/UCuAXFkgsw1L7xaCfnd5JJOw"},
              {"url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ", "live_status": "is_live"}
            ]}
            """);

        var candidate = Assert.Single(SearchCandidatesFrom(document.RootElement));

        Assert.True(candidate.IsLive);
        Assert.Null(candidate.Duration);
    }

    [Fact]
    public void PickMatch_PrefersTheFirstResultCloseToTheSpotifyDuration()
    {
        var match = PickMatch([Candidate("aaaaaaaaaaa", 260), Candidate("bbbbbbbbbbb", 216), Candidate("ccccccccccc", 213)], Spotify.Duration, MaxDuration);

        Assert.Equal("bbbbbbbbbbb", match!.Url.VideoId);
    }

    [Fact]
    public void PickMatch_WithoutACloseDuration_GuessesTheFirstResult()
    {
        var match = PickMatch([Candidate("aaaaaaaaaaa", 260), Candidate("bbbbbbbbbbb", 300)], Spotify.Duration, MaxDuration);

        Assert.Equal("aaaaaaaaaaa", match!.Url.VideoId);
    }

    [Fact]
    public void PickMatch_SkipsLiveAndTooLongVideos()
    {
        var match = PickMatch(
            [Candidate("aaaaaaaaaaa", 214, isLive: true), Candidate("bbbbbbbbbbb", 3600), Candidate("ccccccccccc", 400)],
            Spotify.Duration,
            MaxDuration);

        Assert.Equal("ccccccccccc", match!.Url.VideoId);
    }

    [Fact]
    public void PickMatch_NothingEligible_IsNull()
    {
        Assert.Null(PickMatch([Candidate("aaaaaaaaaaa", 3600)], Spotify.Duration, MaxDuration));
        Assert.Null(PickMatch([], Spotify.Duration, MaxDuration));
    }

    [Fact]
    public void SearchQuery_NamesArtistsAndTitle()
    {
        Assert.Equal("Rick Astley - Never Gonna Give You Up", SearchQuery(Spotify));
        Assert.Equal("A, B - Song", SearchQuery(Spotify with { Title = "Song", Artists = ["A", "B"] }));
        Assert.Equal("Song", SearchQuery(Spotify with { Title = "Song", Artists = [] }));
    }

    [Fact]
    public void Merge_TakesNamesFromSpotify_AndKeepsTheVideosOwnClues()
    {
        var matched = new Track(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            Title: "Rick Astley - Never Gonna Give You Up (Official Video) (4K Remaster)",
            Artist: "Rick Astley",
            Channel: "Rick Astley",
            Duration: TimeSpan.FromSeconds(213),
            PublishedAt: new DateTimeOffset(2009, 10, 25, 0, 0, 0, TimeSpan.Zero),
            Description: "The official video",
            Tags: ["rick astley"]);
        SpotifyUrl.TryParse("spotify:track:4uLU6hMCjMI75M1A2tKUQC", out var url);

        var track = MergeSpotifyTrack(matched, Spotify with { Artists = ["Rick Astley", "Guest"] }, url!);

        Assert.Equal("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC", track.SourceUrl);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", track.MatchedUrl);
        Assert.Equal("Never Gonna Give You Up", track.Title);
        Assert.Equal("Rick Astley, Guest", track.Artist);
        Assert.Equal(Spotify.Duration, track.Duration);
        // A Spotify release date is chosen by the distributor, so the upload date is kept.
        Assert.Equal(matched.PublishedAt, track.PublishedAt);
        Assert.Equal("Rick Astley", track.Channel);
        Assert.Equal("The official video", track.Description);
        Assert.Equal(["rick astley"], track.Tags);
    }

    private static SearchCandidate Candidate(string videoId, double seconds, bool isLive = false)
    {
        YouTubeUrl.TryParse($"https://www.youtube.com/watch?v={videoId}", out var url);
        return new SearchCandidate(url!, TimeSpan.FromSeconds(seconds), isLive);
    }
}
