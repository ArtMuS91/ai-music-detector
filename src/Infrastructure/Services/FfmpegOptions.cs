namespace Infrastructure.Services;

public sealed class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    /// <summary>Executable name or full path. Resolved via PATH when it is a bare name.</summary>
    public string ExecutablePath { get; set; } = "ffmpeg";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Output sample rate in Hz. Kept at CD rate rather than the 16 kHz many speech models
    /// expect: generative-audio artifacts often sit in the upper spectrum, and a detector can
    /// always downsample further but never recover bandwidth that was thrown away here.
    /// </summary>
    public int SampleRate { get; set; } = 44_100;

    /// <summary>Longest slice kept for analysis, taken from the middle of the track. Null keeps the whole track.</summary>
    public TimeSpan? MaxDuration { get; set; } = TimeSpan.FromMinutes(3);
}
