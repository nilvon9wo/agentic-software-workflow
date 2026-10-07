using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Work;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>The item's specification is not on the base branch, so there is nothing to build to.</summary>
[PublicAPI] // the failure's data is its contract with whoever handles it
public sealed record SpecificationNotFound(WorkItemId Item, string Path)
    : ExpectedFailure($"{Item} has no specification at {Path}.");