namespace Infrastructure.Services;

public sealed class YtDlpOptions
{
    public const string SectionName = "YtDlp";

    /// <summary>Executable name or full path. Resolved via PATH when it is a bare name.</summary>
    public string ExecutablePath { get; set; } = "yt-dlp";

    /// <summary>Directory downloaded audio is written to. Defaults to a folder under the system temp path.</summary>
    public string? WorkingDirectory { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Longer videos (and live streams) are skipped instead of downloaded.</summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Downloads larger than this are aborted.</summary>
    public int MaxFileSizeMegabytes { get; set; } = 100;
}
