namespace Core.Services;

/// <summary>Transcribes sung lyrics, as context for the explanation; not a detection signal.</summary>
public interface ILyricsTranscriber
{
    /// <returns>The lyrics, or null when the audio has no recognizable singing (an instrumental, say).</returns>
    Task<string?> TranscribeAsync(PreprocessedAudio audio, CancellationToken cancellationToken = default);
}
