using System.Globalization;
using System.Text.RegularExpressions;
using Core.Models;
using Core.Services;

namespace Analysis.Services;

/// <summary>
/// Reads the clues uploaders leave in a video's own metadata. Checks run in priority order and
/// the first that finds something decides the signal, so its detail always names one reason:
/// <list type="number">
/// <item>An AI tool or AI disclosure named in the title, channel, tags or description. On a
/// pre-2023 upload it gives no signal either way: the video may be about AI rather than made by
/// it, or an early experiment such as OpenAI Jukebox.</item>
/// <item>An upload date before AI song tools became widely available, with no AI mentioned.</item>
/// <item>A claim of being human-made, which costs nothing to write and so counts for little.</item>
/// </list>
/// </summary>
public sealed partial class MetadataHeuristicsSignalProvider : IDetectionSignalProvider
{
    /// <summary>
    /// AI songs existed earlier (OpenAI Jukebox in 2020, AIVA, Mubert), but as rare experiments
    /// rather than tools anyone could use. That changed with voice-cloned "AI covers" in 2023 and
    /// Suno and Udio in late 2023 and 2024, so an older upload that doesn't mention AI is very
    /// likely human-made. Upload dates cannot be backdated.
    /// </summary>
    public static readonly DateTimeOffset AiSongToolsWidespreadFrom = new(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const int MaxReportedMentions = 3;

    // How far before a match to look for a negation such as "not" in "not AI generated".
    private const int NegationLookbehind = 20;

    public string Name => "Metadata clues";

    public Task<Signal> DetectAsync(PreprocessedAudio audio, Track track, CancellationToken cancellationToken = default)
        => Task.FromResult(Detect(track));

    private Signal Detect(Track track)
    {
        var fields = Fields(track).ToList();
        var uploadedEarly = track.PublishedAt is { } published && published < AiSongToolsWidespreadFrom;
        var uploaded = track.PublishedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var aiMentions = FindMentions(fields, AiMention(), negated: false);
        if (aiMentions.Count > 0)
        {
            return uploadedEarly
                ? new Signal(
                    Name,
                    Score: 0.5,
                    Weight: 0,
                    $"Uploaded on {uploaded}, before AI song tools were widespread, but mentions AI: "
                    + $"{Describe(aiMentions)}. It may be about AI or an early AI experiment.")
                : new Signal(Name, Score: 0.9, Weight: 0.6, $"The uploader mentions AI generation: {Describe(aiMentions)}.");
        }

        if (uploadedEarly)
        {
            return new Signal(
                Name,
                Score: 0.05,
                Weight: 0.7,
                $"Uploaded on {uploaded}, before AI song tools became widely available (voice-cloned covers "
                + "in 2023, Suno and Udio in 2023–2024), and the metadata doesn't mention AI.");
        }

        var humanClaims = FindMentions(fields, HumanClaim(), negated: false)
            .Concat(FindMentions(fields, AiMention(), negated: true))
            .ToList();
        if (humanClaims.Count > 0)
        {
            return new Signal(Name, Score: 0.3, Weight: 0.2, $"The uploader claims the track is human-made: {Describe(humanClaims)}.");
        }

        return new Signal(Name, Score: 0.5, Weight: 0, "The title, channel, tags and description give no clue either way.");
    }

    private static IEnumerable<(string Field, string Text)> Fields(Track track)
    {
        (string Field, string? Text)[] fields =
        [
            ("title", track.Title),
            ("artist", track.Artist),
            ("channel", track.Channel),
            ("tags", track.Tags is { Count: > 0 } tags ? string.Join(", ", tags) : null),
            ("description", track.Description),
        ];

        return fields
            .Where(f => !string.IsNullOrWhiteSpace(f.Text))
            .Select(f => (f.Field, f.Text!));
    }

    /// <param name="negated">Keep only matches preceded by a negation ("not AI generated"), or only those that aren't.</param>
    private static List<(string Term, string Field)> FindMentions(
        IEnumerable<(string Field, string Text)> fields,
        Regex pattern,
        bool negated)
    {
        var mentions = new List<(string Term, string Field)>();

        foreach (var (field, text) in fields)
        {
            foreach (Match match in pattern.Matches(text))
            {
                var before = text[Math.Max(0, match.Index - NegationLookbehind)..match.Index];
                if (Negation().IsMatch(before) != negated)
                {
                    continue;
                }

                var term = match.Value.ToLowerInvariant();
                if (!mentions.Contains((term, field)))
                {
                    mentions.Add((term, field));
                }
            }
        }

        return mentions;
    }

    private static string Describe(List<(string Term, string Field)> mentions)
        => string.Join(", ", mentions.Take(MaxReportedMentions).Select(m => $"\"{m.Term}\" in the {m.Field}"));

    [GeneratedRegex(
        @"\b(suno|udio|riffusion|mubert|boomy|soundraw|soundful|aiva|musicgen|stable audio"
        + @"|ai[- ]generated|(?:generated|made|created|produced|composed) (?:by|with|using) (?:an? )?ai"
        + @"|ai (?:music|songs?|tracks?|bands?|artists?|singers?|vocals?|covers?)|aimusic)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AiMention();

    [GeneratedRegex(
        @"\b(?:(?:no|not|without|zero) ai\b|100% human|human[- ](?:made|created|composed))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HumanClaim();

    [GeneratedRegex(@"\b(?:no|not|non|without|never|zero|isn'?t|wasn'?t)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Negation();
}
