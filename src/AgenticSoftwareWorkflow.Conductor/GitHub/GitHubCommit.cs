using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>One commit on a pull request: when it was made, by whom, and its headline.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record GitHubCommit(
    DateTimeOffset? CommittedDate,
    List<GitHubAuthor>? Authors,
    string? MessageHeadline
);