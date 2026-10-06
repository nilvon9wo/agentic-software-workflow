namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// A piece of work and its conversation so far: what was asked for, and every
/// comment since, each marked trusted or not.
/// </summary>
public sealed record WorkItem(
    WorkItemId Id,
    string Title,
    string Body,
    IReadOnlyList<string> Labels,
    IReadOnlyList<WorkComment> Comments
)
{
    /// <summary>The comments that count: those written by maintainers.</summary>
    public IReadOnlyList<WorkComment> TrustedComments => [.. this.Comments.Where(comment => comment.IsTrusted)];
}