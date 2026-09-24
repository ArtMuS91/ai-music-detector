namespace Core.Services;

/// <summary>
/// A failure whose <see cref="Exception.Message"/> is written for the user and is safe to show
/// as the job's failure reason. Any other exception is reported only as a generic per-stage
/// reason, since its message can carry internal details (file paths, tool stderr, SQL errors).
/// </summary>
public class UserFacingException(string message, Exception? innerException = null)
    : Exception(message, innerException);
