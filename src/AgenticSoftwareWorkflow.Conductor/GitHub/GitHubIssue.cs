using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// An issue as <c>gh issue view/list --json</c> prints it. Only the fields the
/// conductor uses are read.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record GitHubIssue(
    int Number,
    string? Title,
    string? Body,
    List<GitHubLabel>? Labels,
    List<GitHubComment>? Comments
);