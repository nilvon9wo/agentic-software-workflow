using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>A command-line tool exited unsuccessfully, or ran past its time limit.</summary>
public sealed record CommandFailed : ExpectedFailure
{
    public CommandFailed(string command, int exitCode, string standardError)
        : base($"'{command}' exited with code {exitCode}: {standardError.Trim()}")
    {
    }
}