using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>One review of a pull request: its verdict, summary, and when it was given.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record GitHubReview(GitHubAuthor? Author, string? Body, string? State, DateTimeOffset? SubmittedAt);