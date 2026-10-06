namespace AgenticSoftwareWorkflow.Conductor.Workflow;

/// <summary>
/// Where each kind of artifact lives in a task's working copy, as glob patterns
/// relative to its root. Roles are granted or denied access by these patterns,
/// so the layout and the rules cannot drift apart.
/// </summary>
public static class WorkspaceLayout
{
    /// <summary>The specification and acceptance criteria.</summary>
    public const string Specification = "spec/**";

    /// <summary>Tests the implementer is shown.</summary>
    public const string VisibleTests = "tests/**";

    /// <summary>Tests withheld from the implementer, to prove the spec was met.</summary>
    public const string HiddenTests = "hidden-tests/**";

    /// <summary>The implementation.</summary>
    public const string Source = "src/**";
}