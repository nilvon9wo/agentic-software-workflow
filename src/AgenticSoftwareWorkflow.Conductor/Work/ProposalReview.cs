namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// What maintainers have said about a proposal since it last changed: the
/// feedback a revision must answer.
/// </summary>
/// <param name="Item">The work item the proposal is for.</param>
/// <param name="Address">Where a human reviews it (a pull request's URL).</param>
/// <param name="Branch">The proposal's branch, which a revision updates.</param>
/// <param name="Feedback">Maintainers' comments since the latest change, oldest first.</param>
public sealed record ProposalReview(
    WorkItemId Item,
    string Address,
    string Branch,
    IReadOnlyList<WorkComment> Feedback
);