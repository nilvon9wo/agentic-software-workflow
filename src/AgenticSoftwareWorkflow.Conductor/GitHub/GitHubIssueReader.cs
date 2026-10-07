using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>Reads <c>gh</c>'s JSON output into issues and pull requests, or explains why it cannot.</summary>
internal static class GitHubIssueReader
{
    private const string EmptyDocument = "the response was the JSON literal null";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Fin<GitHubIssue> ReadIssue(string json) => Read<GitHubIssue>(json);

    public static Fin<List<GitHubIssue>> ReadIssues(string json) => Read<List<GitHubIssue>>(json);

    public static Fin<List<GitHubPullRequest>> ReadPullRequests(string json) => Read<List<GitHubPullRequest>>(json);

    public static Fin<List<GitHubLineComment>> ReadLineComments(string json) => Read<List<GitHubLineComment>>(json);

    private static Fin<T> Read<T>(string json)
        where T : class
    {
        Try<T?> deserialize = Try.lift(() => JsonSerializer.Deserialize<T>(json, Options));
        return deserialize
            .ToFin()
            .MapFail(error => new WorkResponseMalformed(error.Message))
            .Bind(RequirePresent);
    }

    private static Fin<T> RequirePresent<T>(T? value)
        where T : class =>
        value is null
            ? Fin.Fail<T>(new WorkResponseMalformed(EmptyDocument))
            : Fin.Succ(value);
}