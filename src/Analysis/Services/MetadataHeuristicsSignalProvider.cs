using System.Globalization;
using System.Text.RegularExpressions;
using Core.Models;
using Core.Services;

namespace Analysis.Services;

/// <summary>
/// Reads the clues uploaders leave in a video's own metadata. Checks run in priority order and
/// the first that finds something decides the signal, so its detail always names one reason:
/// <list type="number">
/// <item>An upload date before AI tools could make full songs with vocals — a hard fact, since a
/// video's upload date cannot be backdated.</item>
/// <item>An AI tool or AI disclosure named in the title, channel, tags or description.</item>
/// <item>A claim of being human-made, which costs nothing to write and so counts for little.</item>
/// </list>
/// </summary>
public sealed partial class MetadataHeuristicsSignalProvider : IDetectionSignalProvider
{
    /// <summary>
    /// Suno and Udio, the first tools producing convincing full songs, went public in late 2023
    /// and early 2024; voice-cloned "AI covers" took off in 2023. Earlier uploads predate them.
    /// </summary>
    public static readonly DateTimeOffset AiSongToolsAvailableFrom = new(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const int MaxReportedMentions = 3;

    // How far before a match to look for a negation such as "not" in "not AI generated".
    private const int NegationLookbehind = 20;

    public string Name => "Metadata clues";

    public Task<Signal> DetectAsync(PreprocessedAudio audio, Track track, CancellationToken cancellationToken = default)
        => Task.FromResult(Detect(track));

    private Signal Detect(Track track)
    {
        if (track.PublishedAt is { } published && published < AiSongToolsAvailableFrom)
        {
            return new Signal(
                Name,
                Score: 0.05,
                Weight: 0.7,
                $"Uploaded on {published.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}, before AI tools "
                + "could generate full songs with vocals (Suno and Udio launched in 2023–2024).");
        }

        var fields = Fields(track).ToList();

        var aiMentions = FindMentions(fields, AiMention(), negated: false);
        if (aiMentions.Count > 0)
        {
            return new Signal(Name, Score: 0.9, Weight: 0.6, $"The uploader mentions AI generation: {Describe(aiMentions)}.");
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
