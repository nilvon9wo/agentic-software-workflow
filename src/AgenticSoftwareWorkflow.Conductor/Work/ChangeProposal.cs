namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>A pushed branch, offered for review and merging.</summary>
public sealed record ChangeProposal(string Branch, string Title, string Description);