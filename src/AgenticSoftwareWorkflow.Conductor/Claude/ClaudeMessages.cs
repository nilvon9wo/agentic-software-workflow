using System.Text;
using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Agents;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// The messages `claude -p --output-format stream-json` prints, one JSON value
/// after another: the session's progress, any <c>rate_limit_event</c>s, and
/// finally the <c>result</c>. A single JSON document (the plain <c>json</c>
/// format) is the one-message case.
/// </summary>
internal static class ClaudeMessages
{
    private const string TypeProperty = "type";
    private const string ResultType = "result";
    private const string RateLimitEventType = "rate_limit_event";
    private const string RateLimitInfoProperty = "rate_limit_info";
    private const string RejectedStatus = "rejected";
    private const string UnnamedLimit = "unnamed";
    private const string NoResult = "there was no result message";

    public static Fin<IReadOnlyList<JsonElement>> Parse(string output)
    {
        Try<IReadOnlyList<JsonElement>> parse = Try.lift(IReadOnlyList<JsonElement> () => ParseAll(output));
        return parse.ToFin().MapFail(error => new AgentOutputMalformed(error.Message));
    }

    /// <summary>The run's result: the last <c>result</c> message.</summary>
    public static Fin<ClaudeEnvelope> Result(IReadOnlyList<JsonElement> messages) =>
        LatestOfType(messages, ResultType).Match(
            Some: Deserialize<ClaudeEnvelope>,
            None: () => Fin.Fail<ClaudeEnvelope>(new AgentOutputMalformed(NoResult))
        );

    /// <summary>
    /// The usage limit that stopped the run, if the latest rate-limit report
    /// says it was rejected. Earlier reports are history: only the last counts.
    /// </summary>
    public static Option<UsageLimitReached> RejectedLimit(IReadOnlyList<JsonElement> messages) =>
        LatestOfType(messages, RateLimitEventType)
            .Bind(LimitInfo)
            .Filter(info => info.Status == RejectedStatus)
            .Map(ToFailure);

    private static List<JsonElement> ParseAll(string output)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(output), options);
        List<JsonElement> messages = [];
        while (reader.Read())
        {
            messages.Add(JsonElement.ParseValue(ref reader));
        }

        return messages;
    }

    private static Option<JsonElement> LatestOfType(IReadOnlyList<JsonElement> messages, string type)
    {
        List<JsonElement> ofType = [.. messages.Where(message => IsOfType(message, type))];
        return ofType.Count == 0
            ? Option<JsonElement>.None
            : Prelude.Some(ofType[^1]);
    }

    private static bool IsOfType(JsonElement message, string type) =>
        message.ValueKind == JsonValueKind.Object
        && message.TryGetProperty(TypeProperty, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() == type;

    private static Option<ClaudeRateLimitInfo> LimitInfo(JsonElement rateLimitEvent) =>
        rateLimitEvent.TryGetProperty(RateLimitInfoProperty, out JsonElement info)
            ? Deserialize<ClaudeRateLimitInfo>(info).ToOption()
            : Option<ClaudeRateLimitInfo>.None;

    private static UsageLimitReached ToFailure(ClaudeRateLimitInfo info) =>
        new(
            info.RateLimitType ?? UnnamedLimit,
            Prelude.Optional(info.ResetsAt).Map(DateTimeOffset.FromUnixTimeSeconds)
        );

    private static Fin<T> Deserialize<T>(JsonElement element)
        where T : class
    {
        Try<T?> deserialize = Try.lift(() => element.Deserialize<T>());
        return deserialize
            .ToFin()
            .MapFail(error => new AgentOutputMalformed(error.Message))
            .Bind(
                value => value is null
                    ? Fin.Fail<T>(new AgentOutputMalformed($"the {typeof(T).Name} was the JSON literal null"))
                    : Fin.Succ(value)
            );
    }
}