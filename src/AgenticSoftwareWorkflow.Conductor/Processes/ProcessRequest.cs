namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>
/// A process to run: what to start, with which arguments, what to send to its
/// standard input, where, and how long to wait before giving up on it.
/// </summary>
public sealed record ProcessRequest(
    string Executable,
    IReadOnlyList<string> Arguments,
    string StandardInput,
    string WorkingDirectory,
    TimeSpan Timeout
);