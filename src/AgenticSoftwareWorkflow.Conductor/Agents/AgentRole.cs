namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// A job description for one AI run: how capable a model it needs, and which
/// capabilities it is granted. Separation of authority starts here: a role that
/// is not granted <see cref="AgentTool.EditFiles"/> cannot edit.
/// </summary>
public sealed class AgentRole(CapabilityTier tier, IReadOnlyList<AgentTool> tools)
{
    public CapabilityTier Tier { get; } = tier;

    public IReadOnlyList<AgentTool> Tools { get; } = tools;

    public bool CanEdit => this.Tools.Contains(AgentTool.EditFiles);
}