using Analysis.Services;
using Core.Models;
using Core.Services;

namespace Core.Tests.Services;

public class MetadataHeuristicsSignalProviderTests
{
    private static readonly PreprocessedAudio Audio = new("unused.wav", 44_100, 1, TimeSpan.FromMinutes(3));
    private static readonly DateTimeOffset Recent = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly MetadataHeuristicsSignalProvider _provider = new();

    [Fact]
    public async Task UploadBeforeAiSongTools_IsStronglyHuman()
    {
        var signal = await _provider.DetectAsync(Audio, Track(published: new DateTimeOffset(2009, 10, 25, 0, 0, 0, TimeSpan.Zero)));

        Assert.True(signal.Score < 0.1);
        Assert.True(signal.Weight >= 0.7);
        Assert.Contains("2009-10-25", signal.Detail);
    }

    [Fact]
    public async Task OldUploadDate_WinsOverAiKeywords()
    {
        // A 2019 song titled "AI Song" is about AI; it could not have been made by Suno.
        var signal = await _provider.DetectAsync(
            Audio,
            Track(title: "AI Song", published: new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.True(signal.Score < 0.1);
    }

    [Theory]
    [InlineData("Midnight Drive (Suno v4.5)", null)]
    [InlineData("Midnight Drive", "Made with Udio. Lyrics by me.")]
    [InlineData("Midnight Drive | AI Generated Music", null)]
    [InlineData("Midnight Drive", "This song was created using AI.")]
    [InlineData("Drake - Midnight (AI Cover)", null)]
    public async Task AiDisclosure_ScoresAi(string title, string? description)
    {
        var signal = await _provider.DetectAsync(Audio, Track(title: title, description: description));

        Assert.True(signal.Score > 0.8);
        Assert.True(signal.Weight > 0.5);
    }

    [Fact]
    public async Task AiDisclosure_NamesWhereItWasFound()
    {
        var signal = await _provider.DetectAsync(Audio, Track(tags: ["lofi", "Suno"]));

        Assert.Contains("\"suno\" in the tags", signal.Detail);
    }

    [Theory]
    [InlineData("100% human made, no AI involved.")]
    [InlineData("This is NOT AI generated, I recorded every part myself.")]
    [InlineData("Written and produced without AI.")]
    public async Task HumanClaim_LeansHumanButCountsForLittle(string description)
    {
        var signal = await _provider.DetectAsync(Audio, Track(description: description));

        Assert.True(signal.Score < 0.5);
        Assert.True(signal.Weight <= 0.2);
    }

    [Theory]
    [InlineData("Said the Sky - Ai Ai Ai")]
    [InlineData("Tsunoda highlights")]
    [InlineData("Studio session")]
    public async Task WordsThatOnlyContainKeywords_DoNotMatch(string title)
    {
        var signal = await _provider.DetectAsync(Audio, Track(title: title));

        Assert.Equal(0, signal.Weight);
    }

    [Fact]
    public async Task NoClues_GivesNoSignal()
    {
        var signal = await _provider.DetectAsync(Audio, Track(title: "Midnight Drive", description: "Official audio."));

        Assert.Equal(0.5, signal.Score);
        Assert.Equal(0, signal.Weight);
    }

    private static Track Track(
        string? title = "Midnight Drive",
        string? description = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? published = null)
        => new(
            "https://www.youtube.com/watch?v=abc123",
            Title: title,
            Artist: "Some Artist",
            Channel: "Some Artist",
            PublishedAt: published ?? Recent,
            Description: description,
            Tags: tags);
}
