using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>A work item was handed to a source it does not belong to.</summary>
public sealed record ForeignWorkItem : ExpectedFailure
{
    public ForeignWorkItem(WorkItemId id, string source)
        : base($"Work item {id} does not belong to {source}.")
    {
    }
}