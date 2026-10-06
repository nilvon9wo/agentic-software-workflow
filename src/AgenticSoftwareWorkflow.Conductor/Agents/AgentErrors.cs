using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// The ways an agent run can fail that the workflow is expected to handle. Each
/// has a stable code, so the workflow can choose a response (retry, repair,
/// escalate) without parsing messages.
/// </summary>
public static class AgentErrors
{
    public const int TimedOutCode = 1001;
    public const int ProcessFailedCode = 1002;
    public const int MalformedOutputCode = 1003;
    public const int AgentReportedErrorCode = 1004;

    public static Error TimedOut(TimeSpan timeout) =>
        Error.New(TimedOutCode, $"The agent did not finish within {timeout}.");

    public static Error ProcessFailed(int exitCode, string standardError) =>
        Error.New(ProcessFailedCode, $"The agent process exited with code {exitCode}: {standardError.Trim()}");

    public static Error MalformedOutput(string detail) =>
        Error.New(MalformedOutputCode, $"The agent's output could not be read: {detail}");

    public static Error AgentReportedError(string kind, string message) =>
        Error.New(AgentReportedErrorCode, $"The agent reported an error ({kind}): {message}");
}