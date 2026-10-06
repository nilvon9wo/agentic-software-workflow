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
}