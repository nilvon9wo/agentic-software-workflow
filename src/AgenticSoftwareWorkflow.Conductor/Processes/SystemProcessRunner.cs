using System.Diagnostics;

namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>
/// Runs a real operating-system process, capturing its output and stopping it
/// (with any children it started) if it outlives its timeout.
/// </summary>
public sealed class SystemProcessRunner : IProcessCapable
{
    private const int TimedOutExitCode = -1;

    public async Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        using Process process = Start(request);
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.StandardInput.WriteAsync(request.StandardInput.AsMemory(), cancellationToken);
        process.StandardInput.Close();
        bool hasExited = await HasExitedWithinAsync(process, request.Timeout, cancellationToken);
        return hasExited
            ? await CollectAsync(process, standardOutput, standardError)
            : Stop(process);
    }

    private static Process Start(ProcessRequest request)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = request.Executable,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Process.Start returns null only when it reuses an already-running
        // process, which is impossible with UseShellExecute = false; a start
        // failure throws instead.
        return Process.Start(startInfo)!;
    }

    private static async Task<bool> HasExitedWithinAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Only our own timeout lands here; the caller's cancellation propagates.
            return false;
        }
    }

    private static async Task<ProcessOutcome> CollectAsync(
        Process process,
        Task<string> standardOutput,
        Task<string> standardError
    )
    {
        string output = await standardOutput;
        string error = await standardError;
        return new ProcessOutcome(process.ExitCode, output, error, false);
    }

    private static ProcessOutcome Stop(Process process)
    {
        process.Kill(entireProcessTree: true);
        return new ProcessOutcome(TimedOutExitCode, string.Empty, string.Empty, true);
    }
}