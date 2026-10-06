using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>The agent's process failed without producing a readable answer.</summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
public sealed record AgentProcessFailed(int ExitCode, string StandardError)
    : ExpectedFailure($"The agent process exited with code {ExitCode}: {StandardError.Trim()}");