using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>The agent itself reported that it failed — ran out of turns, say.</summary>
public sealed record AgentReportedError : ExpectedFailure
{
    public AgentReportedError(string kind, string detail)
        : base($"The agent reported an error ({kind}): {detail}")
    {
    }
}