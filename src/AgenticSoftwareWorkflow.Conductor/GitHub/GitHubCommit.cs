using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>One commit on a pull request; only when it was made matters here.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record GitHubCommit(DateTimeOffset? CommittedDate);