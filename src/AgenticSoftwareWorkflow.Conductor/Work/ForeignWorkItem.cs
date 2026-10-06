using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>A work item was handed to a source it does not belong to.</summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
public sealed record ForeignWorkItem(WorkItemId Id, string Source)
    : ExpectedFailure($"Work item {Id} does not belong to {Source}.");