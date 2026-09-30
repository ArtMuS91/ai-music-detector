using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Models;
using Infrastructure.Services;

namespace Core.Tests.Services;

/// <summary>
/// Maps a real yt-dlp <c>--dump-json</c> object (Fixtures/yt-dlp-dump.json, trimmed of the large
/// format and thumbnail lists), so a renamed key or changed shape shows up here instead of as
/// signals quietly finding "no clue".
/// </summary>
public class YtDlpMetadataTests
{
    private const string SourceUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    [Fact]
    public void RealDump_MapsEveryTrackField()
    {
        var track = Map(Fixture());

        Assert.Equal(SourceUrl, track.SourceUrl);
        Assert.Equal("Rick Astley - Never Gonna Give You Up (Official Video) (4K Remaster)", track.Title);
        // "track"/"artist" are null for this video, so title and uploader stand in for them.
        Assert.Equal("Rick Astley", track.Artist);
        Assert.Equal("Rick Astley", track.Channel);
        Assert.Equal(TimeSpan.FromSeconds(213), track.Duration);
        Assert.Equal(new DateTimeOffset(2009, 10, 25, 0, 0, 0, TimeSpan.Zero), track.PublishedAt);
        Assert.StartsWith("The official video for", track.Description);
        Assert.Contains("rick astley", track.Tags!);
        Assert.Contains("Never Gonna Give You Up", track.Tags!);
    }

    [Fact]
    public void MusicMetadata_WinsOverVideoTitleAndUploader()
    {
        var json = Fixture();
        json["track"] = "Never Gonna Give You Up";
        json["artist"] = "Rick Astley";
        json["uploader"] = "Some Reupload Channel";

        var track = Map(json);

        Assert.Equal("Never Gonna Give You Up", track.Title);
        Assert.Equal("Rick Astley", track.Artist);
    }

    [Fact]
    public void MissingTags_AreNull()
    {
        var json = Fixture();
        json.Remove("tags");

        Assert.Null(Map(json).Tags);
    }

    [Fact]
    public void NonStringTagEntries_AreSkipped()
    {
        var json = Fixture();
        json["tags"] = new JsonArray("lofi", 42, null, new JsonObject(), "Suno");

        Assert.Equal(["lofi", "Suno"], Map(json).Tags);
    }

    [Fact]
    public void TagsOfTheWrongShape_AreNull()
    {
        var json = Fixture();
        json["tags"] = "lofi, Suno";

        Assert.Null(Map(json).Tags);
    }

    [Fact]
    public void MissingOrNullFields_StayNull()
    {
        var json = new JsonObject { ["id"] = "dQw4w9WgXcQ", ["description"] = null };

        var track = Map(json);

        Assert.Equal(new Track(SourceUrl), track);
    }

    private static JsonObject Fixture()
        => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "yt-dlp-dump.json")))!.AsObject();

    private static Track Map(JsonObject json)
    {
        using var document = JsonDocument.Parse(json.ToJsonString());
        return YtDlpAudioAcquisitionService.TrackFromMetadata(document.RootElement, SourceUrl);
    }
}
