using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>The agent finished, but what it printed could not be read.</summary>
public sealed record AgentOutputMalformed : ExpectedFailure
{
    public AgentOutputMalformed(string detail)
        : base($"The agent's output could not be read: {detail}")
    {
    }
}