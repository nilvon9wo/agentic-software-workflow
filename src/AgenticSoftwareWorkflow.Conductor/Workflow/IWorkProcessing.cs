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
}