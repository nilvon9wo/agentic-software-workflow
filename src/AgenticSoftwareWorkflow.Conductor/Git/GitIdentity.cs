namespace AgenticSoftwareWorkflow.Conductor.Git;

/// <summary>
/// Who commits the workers' changes: the workers' own account, never the
/// maintainer's, so history shows what was automated and what was not.
/// </summary>
public sealed record GitIdentity(string Name, string Email);