using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Work;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>What one build works from: the item, its approved specification, and its workspace.</summary>
public sealed record BuildJob(WorkItem Item, string Specification, Workspace Workspace);