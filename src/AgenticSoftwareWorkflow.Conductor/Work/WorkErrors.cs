using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// The ways reaching a work source can fail that the workflow is expected to
/// handle, each with a stable code.
/// </summary>
public static class WorkErrors
{
    public const int CommandFailedCode = 2001;
    public const int MalformedResponseCode = 2002;
    public const int ForeignItemCode = 2003;

    public static Error CommandFailed(string command, int exitCode, string standardError) =>
        Error.New(CommandFailedCode, $"'{command}' exited with code {exitCode}: {standardError.Trim()}");

    public static Error MalformedResponse(string detail) =>
        Error.New(MalformedResponseCode, $"The work source's response could not be read: {detail}");

    public static Error ForeignItem(WorkItemId id, string source) =>
        Error.New(ForeignItemCode, $"Work item {id} does not belong to {source}.");
}