using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>The agent did not finish within its task's time limit.</summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
public sealed record AgentTimedOut(TimeSpan Timeout)
    : ExpectedFailure($"The agent did not finish within {Timeout}.");