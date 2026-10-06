using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>The agent finished, but what it printed could not be read.</summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
public sealed record AgentOutputMalformed(string Detail)
    : ExpectedFailure($"The agent's output could not be read: {Detail}");