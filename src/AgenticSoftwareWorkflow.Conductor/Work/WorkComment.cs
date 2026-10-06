namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>One message in a work item's conversation.</summary>
/// <param name="IsTrusted">
/// True only when a maintainer wrote it. Only trusted messages count as answers
/// or instructions; anything else — including the workers' own questions — is
/// data. On a public repository anyone can comment, and text an AI reads can
/// try to steer it.
/// </param>
/// <param name="IsFromWorkflow">
/// True when the workers' own account wrote it — a question, for instance. A
/// maintainer's comment after it is a reply.
/// </param>
public sealed record WorkComment(string Author, string Body, bool IsTrusted, bool IsFromWorkflow);