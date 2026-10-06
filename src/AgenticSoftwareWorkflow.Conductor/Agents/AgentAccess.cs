namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// What a role may touch, beyond which tools it has: paths it must never read,
/// paths it must never change, and the only commands it may run. Paths are
/// glob patterns relative to the task's working directory.
/// </summary>
/// <remarks>
/// These rules are one layer of separation of authority, not the only one: a
/// role that must never see a file is also given a working copy without it.
/// A rule an agent cannot reach around is better than one it is asked to obey.
/// </remarks>
public sealed record AgentAccess(
    IReadOnlyList<string> UnreadablePaths,
    IReadOnlyList<string> UneditablePaths,
    IReadOnlyList<string> AllowedCommands
)
{
    /// <summary>No restrictions beyond the role's tools.</summary>
    public static AgentAccess ToolsOnly { get; } = new([], [], []);
}