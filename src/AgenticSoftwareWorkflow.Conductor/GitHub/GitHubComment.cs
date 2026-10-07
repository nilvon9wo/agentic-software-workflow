using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>One comment on an issue or pull request; a deleted account has no author.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record GitHubComment(GitHubAuthor? Author, string? Body, DateTimeOffset? CreatedAt);