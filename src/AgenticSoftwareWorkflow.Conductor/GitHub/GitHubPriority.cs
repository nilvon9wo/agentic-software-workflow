namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// How urgently a maintainer wants an issue worked on, from its
/// <c>priority: high</c> or <c>priority: low</c> label; issues are taken in
/// this order.
/// </summary>
internal enum GitHubPriority
{
    /// <summary>Labelled <c>priority: high</c>: taken before anything else.</summary>
    High,

    /// <summary>No priority label.</summary>
    Normal,

    /// <summary>Labelled <c>priority: low</c>: taken only when nothing else is waiting.</summary>
    Low,
}