using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>A command-line tool exited unsuccessfully, or ran past its time limit.</summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
public sealed record CommandFailed(string Command, int ExitCode, string StandardError)
    : ExpectedFailure($"'{Command}' exited with code {ExitCode}: {StandardError.Trim()}");