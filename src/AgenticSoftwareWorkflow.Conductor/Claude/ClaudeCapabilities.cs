using System.Collections.Frozen;
using AgenticSoftwareWorkflow.Conductor.Agents;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// How vendor-neutral roles translate into Claude Code: which model alias serves
/// each capability tier, and which built-in tools grant each capability.
/// </summary>
/// <remarks>
/// Model aliases (not dated model ids) follow Claude Code's own default for each
/// family, so a newer model is picked up without a code change.
/// </remarks>
internal static class ClaudeCapabilities
{
    private static readonly FrozenDictionary<CapabilityTier, string> ModelAliases =
        new Dictionary<CapabilityTier, string>
        {
            [CapabilityTier.Small] = "haiku",
            [CapabilityTier.Standard] = "sonnet",
            [CapabilityTier.Strongest] = "opus",
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<AgentTool, string[]> ToolNames =
        new Dictionary<AgentTool, string[]>
        {
            [AgentTool.ReadFiles] = ["Read"],
            [AgentTool.SearchFiles] = ["Grep", "Glob"],
            [AgentTool.EditFiles] = ["Edit", "Write"],
            [AgentTool.RunCommands] = ["Bash"],
        }.ToFrozenDictionary();

    public static string ModelFor(CapabilityTier tier) =>
        ModelAliases.TryGetValue(tier, out string? alias)
            ? alias
            : throw new ArgumentOutOfRangeException(nameof(tier), tier, null);

    public static IReadOnlyList<string> ToolNamesFor(IReadOnlyList<AgentTool> tools) =>
        [.. tools.SelectMany(ToolNamesFor)];

    private static string[] ToolNamesFor(AgentTool tool) =>
        ToolNames.TryGetValue(tool, out string[]? names)
            ? names
            : throw new ArgumentOutOfRangeException(nameof(tool), tool, null);
}