using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// A comment on one line of a pull request's diff, as GitHub's REST API
/// returns it (<c>gh api repos/…/pulls/N/comments</c>, which uses snake case).
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record GitHubLineComment(
    [property: JsonPropertyName("user")] GitHubAuthor? User,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("line")] int? Line,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt
);