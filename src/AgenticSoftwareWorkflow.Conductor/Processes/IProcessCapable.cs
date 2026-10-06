namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>
/// The port through which the conductor starts external processes, so that the
/// logic deciding what to run is tested without running anything.
/// </summary>
public interface IProcessCapable
{
    /// <summary>Runs the process to completion, or stops it at its timeout.</summary>
    /// <exception cref="System.ComponentModel.Win32Exception">
    /// The executable cannot be started — a misconfiguration, reported loudly.
    /// </exception>
    Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}