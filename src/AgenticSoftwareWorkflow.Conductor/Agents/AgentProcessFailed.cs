using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>The agent's process failed without producing a readable answer.</summary>
public sealed record AgentProcessFailed : ExpectedFailure
{
    public AgentProcessFailed(int exitCode, string standardError)
        : base($"The agent process exited with code {exitCode}: {standardError.Trim()}")
    {
    }
}