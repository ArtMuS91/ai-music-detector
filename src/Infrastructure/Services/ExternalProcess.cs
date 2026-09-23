using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ExternalToolException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Runs a command-line tool (yt-dlp, ffmpeg) to completion, capturing its output.
/// Start failures and timeouts surface as <see cref="ExternalToolException"/>; caller
/// cancellation surfaces as <see cref="OperationCanceledException"/>. Either way the
/// process tree is killed so no orphaned tool keeps writing files.
/// </summary>
internal static class ExternalProcess
{
    public sealed record Result(int ExitCode, string Stdout, string Stderr);

    public static async Task<Result> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new ExternalToolException($"Could not start '{fileName}'. Is it installed and on PATH?", ex);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var stdout = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process, fileName, logger);
            throw new ExternalToolException($"'{Path.GetFileName(fileName)}' timed out after {timeout}.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process, fileName, logger);
            throw;
        }

        return new Result(process.ExitCode, await stdout, await stderr);
    }

    /// <summary>Last non-empty stderr line — tools print the actual error last.</summary>
    public static string Summarize(string stderr)
    {
        var lastLine = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return string.IsNullOrEmpty(lastLine) ? "no error output" : lastLine;
    }

    private static void TryKill(Process process, string fileName, ILogger logger)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not kill the {Tool} process after cancellation.", fileName);
        }
    }
}
