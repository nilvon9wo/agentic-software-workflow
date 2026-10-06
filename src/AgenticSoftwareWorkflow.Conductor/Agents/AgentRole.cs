namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// A job description for one AI run: how capable a model it needs, which
/// capabilities it is granted, and what it may touch. Separation of authority
/// starts here: a role that is not granted <see cref="AgentTool.EditFiles"/>
/// cannot edit, and one whose <see cref="Access"/> forbids a path cannot reach it.
/// </summary>
public sealed class AgentRole(CapabilityTier tier, IReadOnlyList<AgentTool> tools, AgentAccess access)
{
    public CapabilityTier Tier { get; } = tier;

    public IReadOnlyList<AgentTool> Tools { get; } = tools;

    public AgentAccess Access { get; } = access;

    public bool CanEdit => this.Tools.Contains(AgentTool.EditFiles);
}