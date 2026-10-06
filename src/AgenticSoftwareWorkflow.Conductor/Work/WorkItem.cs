namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// A piece of work and its conversation so far: what was asked for, and every
/// comment since, each marked trusted or not.
/// </summary>
/// <param name="IsWaiting">True while the item waits on a maintainer's answer.</param>
public sealed record WorkItem(
    WorkItemId Id,
    string Title,
    string Body,
    IReadOnlyList<string> Labels,
    IReadOnlyList<WorkComment> Comments,
    bool IsWaiting
)
{
    /// <summary>
    /// The comments a worker may read: maintainers' and the workflow's own
    /// questions, so each answer arrives with what it answers. Anyone else's
    /// text is left out.
    /// </summary>
    public IReadOnlyList<WorkComment> Conversation =>
        [.. this.Comments.Where(comment => comment.IsTrusted || comment.IsFromWorkflow)];

    /// <summary>
    /// Waiting, but a maintainer has replied since the workflow last spoke: the
    /// wait is over. A stranger's comment is not a reply.
    /// </summary>
    public bool IsAnswered => this.IsWaiting && this.CommentsSinceTheWorkflowSpoke.Any(comment => comment.IsTrusted);

    private IEnumerable<WorkComment> CommentsSinceTheWorkflowSpoke =>
        this.Comments
            .Reverse()
            .TakeWhile(comment => !comment.IsFromWorkflow);
}