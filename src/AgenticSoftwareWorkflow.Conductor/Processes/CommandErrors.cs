using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>How running a command-line tool can fail, with a stable code.</summary>
public static class CommandErrors
{
    public const int FailedCode = 4001;

    public static Error Failed(string command, int exitCode, string standardError) =>
        Error.New(FailedCode, $"'{command}' exited with code {exitCode}: {standardError.Trim()}");
}