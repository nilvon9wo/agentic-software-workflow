using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// A pull request as <c>gh pr list --json</c> prints it. Only the fields the
/// conductor uses are read.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record GitHubPullRequest(
    int Number,
    string? Url,
    string? Body,
    string? HeadRefName,
    string? ReviewDecision,
    List<GitHubReview>? Reviews,
    List<GitHubComment>? Comments,
    List<GitHubCommit>? Commits
);