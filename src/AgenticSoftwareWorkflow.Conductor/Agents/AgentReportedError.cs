using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>The agent itself reported that it failed — ran out of turns, say.</summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
public sealed record AgentReportedError(string Kind, string Detail)
    : ExpectedFailure($"The agent reported an error ({Kind}): {Detail}");