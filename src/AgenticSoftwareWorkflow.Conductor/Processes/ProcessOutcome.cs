namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>
/// How a process ended: its exit code and everything it printed, or the fact
/// that it was stopped for running past its timeout.
/// </summary>
public sealed record ProcessOutcome(int ExitCode, string StandardOutput, string StandardError, bool HasTimedOut);