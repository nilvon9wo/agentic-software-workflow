using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// The port through which finished work is offered for merging. A GitHub pull
/// request is one adapter. Whether a proposal then merges by itself or waits
/// for a human is the repository's rule, not the conductor's: the conductor
/// asks for it to merge once every rule is satisfied.
/// </summary>
public interface IChangeProposing
{
    /// <summary>Proposes the change; the result is where a human can review it.</summary>
    Task<Fin<string>> Propose(ChangeProposal proposal, CancellationToken cancellationToken);

    /// <summary>
    /// The work items whose open proposals a maintainer has commented on since
    /// they last changed, and has not approved: each needs a revision.
    /// </summary>
    Task<Fin<IReadOnlyList<WorkItemId>>> ListAwaitingRevision(CancellationToken cancellationToken);

    /// <summary>
    /// The work items with a specification proposal still open: their
    /// specification is being written or revised, so nothing may be built yet.
    /// </summary>
    Task<Fin<IReadOnlyList<WorkItemId>>> ListOpenSpecifications(CancellationToken cancellationToken);

    /// <summary>The open proposal for the item, and the feedback since it last changed.</summary>
    Task<Fin<ProposalReview>> ReadReview(WorkItemId id, CancellationToken cancellationToken);

    /// <summary>
    /// Brings every open proposal that has fallen behind the base branch up to
    /// date, so none waits on a human to do it; what happened to each.
    /// </summary>
    Task<Fin<IReadOnlyList<string>>> UpdateBehind(CancellationToken cancellationToken);

    /// <summary>Answers on the proposal itself, where its reviewers will see it.</summary>
    Task<Fin<Unit>> Reply(ProposalReview review, string message, CancellationToken cancellationToken);
}