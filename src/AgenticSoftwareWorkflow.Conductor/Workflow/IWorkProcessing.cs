using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Workflow;

/// <summary>
/// Takes one work item as far as the pipeline can take it now. The run loop
/// depends on this, not on any one stage, so stages can be added behind it.
/// </summary>
public interface IWorkProcessing
{
    /// <summary>What happened, in a sentence fit for a person to read.</summary>
    Task<Fin<string>> Run(WorkItemId id, CancellationToken cancellationToken);

    /// <summary>The items whose proposals a maintainer has reviewed since they last changed.</summary>
    Task<Fin<IReadOnlyList<WorkItemId>>> ListAwaitingRevision(CancellationToken cancellationToken);

    /// <summary>Revises the item's proposal in response to its review; what happened, for a person.</summary>
    Task<Fin<string>> Revise(WorkItemId id, CancellationToken cancellationToken);
}