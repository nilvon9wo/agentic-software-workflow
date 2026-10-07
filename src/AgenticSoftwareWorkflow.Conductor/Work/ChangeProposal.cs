namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>A pushed branch, offered for review and merging, for one work item.</summary>
public sealed record ChangeProposal(WorkItemId Item, string Branch, string Title, string Description);