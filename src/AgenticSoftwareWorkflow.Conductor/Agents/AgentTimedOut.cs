using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>The agent did not finish within its task's time limit.</summary>
public sealed record AgentTimedOut : ExpectedFailure
{
    public AgentTimedOut(TimeSpan timeout)
        : base($"The agent did not finish within {timeout}.")
    {
    }
}