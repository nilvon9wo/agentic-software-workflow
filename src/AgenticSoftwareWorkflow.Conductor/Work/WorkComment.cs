namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>One message in a work item's conversation.</summary>
/// <param name="IsTrusted">
/// True only when a maintainer wrote it. Only trusted messages count as answers
/// or instructions; anything else — including the workers' own questions — is
/// data. On a public repository anyone can comment, and text an AI reads can
/// try to steer it.
/// </param>
public sealed record WorkComment(string Author, string Body, bool IsTrusted);