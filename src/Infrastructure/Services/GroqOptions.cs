namespace Infrastructure.Services;

public sealed class GroqOptions
{
    public const string SectionName = "Groq";

    /// <summary>Secret — supply via the <c>Groq__ApiKey</c> environment variable (see docker-compose.yml), never appsettings.</summary>
    public string? ApiKey { get; set; }

    public Uri BaseUrl { get; set; } = new("https://api.groq.com/openai/v1/");

    /// <summary>Must be a model that supports Groq's built-in <c>browser_search</c> tool (the GPT-OSS family).</summary>
    public string Model { get; set; } = "openai/gpt-oss-120b";

    /// <summary>Writes the result's explanation; no tools, so any chat model works.</summary>
    public string ExplanationModel { get; set; } = "openai/gpt-oss-120b";

    /// <summary>Whisper model that transcribes lyrics for the explanation.</summary>
    public string TranscriptionModel { get; set; } = "whisper-large-v3-turbo";

    /// <summary>
    /// Groq's upload cap for transcription on the free tier. Three minutes of 44.1 kHz mono 16-bit
    /// WAV is about 16 MB; a longer preprocessing window could exceed it.
    /// </summary>
    public int MaxTranscriptionUploadMegabytes { get; set; } = 25;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>Total tries when Groq fails transiently (rate limit, malformed tool call), waiting out any Retry-After in between.</summary>
    public int MaxAttempts { get; set; } = 3;
}
