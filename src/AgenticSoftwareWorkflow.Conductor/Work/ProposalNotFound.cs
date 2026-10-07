using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>No open proposal exists for the work item (it merged or was closed meanwhile).</summary>
[PublicAPI] // the failure's data is its contract with whoever handles it
public sealed record ProposalNotFound(WorkItemId Item)
    : ExpectedFailure($"There is no open proposal for {Item}.");