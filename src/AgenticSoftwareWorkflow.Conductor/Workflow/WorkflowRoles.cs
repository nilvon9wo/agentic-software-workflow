using AgenticSoftwareWorkflow.Conductor.Agents;

namespace AgenticSoftwareWorkflow.Conductor.Workflow;

/// <summary>
/// Every role in the pipeline, and exactly what each may do. This is the
/// separation-of-authority table from docs/how-it-works/architecture.md,
/// expressed as the code that enforces it.
/// </summary>
public static class WorkflowRoles
{
    private const string Build = "dotnet build";
    private const string Test = "dotnet test";
    private const string Format = "dotnet format";

    private static readonly AgentTool[] ReadOnly = [AgentTool.ReadFiles, AgentTool.SearchFiles];
    private static readonly AgentTool[] ReadAndEdit = [AgentTool.ReadFiles, AgentTool.SearchFiles, AgentTool.EditFiles];
    private static readonly AgentTool[] ReadEditAndRun =
        [AgentTool.ReadFiles, AgentTool.SearchFiles, AgentTool.EditFiles, AgentTool.RunCommands];

    /// <summary>Labels, de-duplicates, and judges readiness: pure reasoning over the text it is given.</summary>
    public static AgentRole Triage { get; } = new(CapabilityTier.Small, [], AgentAccess.ToolsOnly);

    /// <summary>Writes the specification; touches nothing else.</summary>
    public static AgentRole Specifier { get; } = new(
        CapabilityTier.Standard,
        ReadAndEdit,
        new AgentAccess(
            [],
            [WorkspaceLayout.Source, WorkspaceLayout.VisibleTests, WorkspaceLayout.HiddenTests],
            []
        )
    );

    /// <summary>Writes visible and hidden tests from the specification; cannot change it.</summary>
    public static AgentRole TestAuthor { get; } = new(
        CapabilityTier.Standard,
        ReadEditAndRun,
        new AgentAccess([], [WorkspaceLayout.Specification, WorkspaceLayout.Source], [Build, Test])
    );

    /// <summary>Judges the tests; reads everything, changes nothing.</summary>
    public static AgentRole TestReviewer { get; } = new(CapabilityTier.Standard, ReadOnly, AgentAccess.ToolsOnly);

    /// <summary>
    /// Builds the solution from the specification and the visible tests. It can
    /// neither see the hidden tests nor change the spec or any test — it cannot
    /// redefine success.
    /// </summary>
    public static AgentRole Implementer { get; } = new(
        CapabilityTier.Standard,
        ReadEditAndRun,
        new AgentAccess(
            [WorkspaceLayout.HiddenTests],
            [WorkspaceLayout.Specification, WorkspaceLayout.VisibleTests, WorkspaceLayout.HiddenTests],
            [Build, Test, Format]
        )
    );

    /// <summary>Judges the implementation; reads everything, changes nothing.</summary>
    public static AgentRole CodeReviewer { get; } = new(CapabilityTier.Standard, ReadOnly, AgentAccess.ToolsOnly);

    /// <summary>
    /// After repeated failure, decides whether the spec, the tests, or the code
    /// is wrong. The hardest judgement, so the strongest tier; it only reads.
    /// </summary>
    public static AgentRole Arbitrator { get; } = new(CapabilityTier.Strongest, ReadOnly, AgentAccess.ToolsOnly);
}