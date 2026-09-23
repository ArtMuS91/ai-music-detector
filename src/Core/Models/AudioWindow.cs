namespace Core.Models;

/// <summary>The slice of a track that preprocessing keeps for analysis.</summary>
public readonly record struct AudioWindow(TimeSpan Start, TimeSpan Length)
{
    /// <summary>
    /// Picks at most <paramref name="maxLength"/> from the middle of the track, where
    /// the arrangement is densest — intros and fade-outs carry little signal and would
    /// dilute a capped window. With an unknown duration the window starts at zero.
    /// </summary>
    public static AudioWindow Centered(TimeSpan? trackDuration, TimeSpan maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxLength, TimeSpan.Zero);

        if (trackDuration is not { } duration || duration <= maxLength)
        {
            return new AudioWindow(TimeSpan.Zero, maxLength);
        }

        return new AudioWindow((duration - maxLength) / 2, maxLength);
    }
}
