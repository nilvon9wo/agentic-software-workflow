using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;
using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// Turns how a `claude -p` process ended into the agent's result, or into the
/// <see cref="AgentErrors"/> failure that explains why there is none.
/// </summary>
internal static class ClaudeOutputReader
{
    private const string DeniedToolProperty = "tool_name";
    private const string UnknownErrorKind = "unknown";
    private const string EmptyDocument = "the output was the JSON literal null";

    public static Fin<AgentResult> Read(ProcessOutcome outcome, TimeSpan timeout) =>
        outcome.HasTimedOut
            ? Fin.Fail<AgentResult>(AgentErrors.TimedOut(timeout))
            : ReadEnvelope(outcome).Bind(ToResult);

    private static Fin<ClaudeEnvelope> ReadEnvelope(ProcessOutcome outcome)
    {
        Fin<ClaudeEnvelope> envelope = Parse(outcome.StandardOutput);
        return outcome.ExitCode == 0
            ? envelope
            : envelope.MapFail(_ => AgentErrors.ProcessFailed(outcome.ExitCode, outcome.StandardError));
    }

    private static Fin<ClaudeEnvelope> Parse(string standardOutput)
    {
        Try<ClaudeEnvelope?> deserialize = Try.lift(
            () => JsonSerializer.Deserialize<ClaudeEnvelope>(standardOutput)
        );
        return deserialize
            .ToFin()
            .MapFail(error => AgentErrors.MalformedOutput(error.Message))
            .Bind(RequirePresent);
    }

    private static Fin<ClaudeEnvelope> RequirePresent(ClaudeEnvelope? envelope) =>
        envelope is null
            ? Fin.Fail<ClaudeEnvelope>(AgentErrors.MalformedOutput(EmptyDocument))
            : Fin.Succ(envelope);

    private static Fin<AgentResult> ToResult(ClaudeEnvelope envelope) =>
        envelope.IsError
            ? Fin.Fail<AgentResult>(ReportedError(envelope))
            : Fin.Succ(Describe(envelope));

    private static Error ReportedError(ClaudeEnvelope envelope) =>
        AgentErrors.AgentReportedError(envelope.Subtype ?? UnknownErrorKind, envelope.Result ?? string.Empty);

    private static AgentResult Describe(ClaudeEnvelope envelope) =>
        new(
            envelope.Result ?? string.Empty,
            StructuredOutputOf(envelope),
            UsageOf(envelope),
            DeniedToolsOf(envelope)
        );

    private static Option<string> StructuredOutputOf(ClaudeEnvelope envelope) =>
        Prelude.Optional(envelope.StructuredOutput?.GetRawText());

    private static AgentUsage UsageOf(ClaudeEnvelope envelope)
    {
        ClaudeUsage usage = envelope.Usage ?? new ClaudeUsage(0, 0);
        IReadOnlyList<string> models = [.. envelope.ModelUsage?.Keys ?? Enumerable.Empty<string>()];
        return new AgentUsage(
            usage.InputTokens,
            usage.OutputTokens,
            envelope.TotalCostUsd,
            envelope.NumberOfTurns,
            models
        );
    }

    private static IReadOnlyList<string> DeniedToolsOf(ClaudeEnvelope envelope) =>
        [.. (envelope.PermissionDenials ?? []).Select(DescribeDenial)];

    /// <summary>
    /// The denied tool's name when the entry carries one; otherwise the entry
    /// itself, so an unfamiliar shape is reported rather than lost.
    /// </summary>
    private static string DescribeDenial(JsonElement denial)
    {
        bool hasToolName = denial.ValueKind == JsonValueKind.Object
            && denial.TryGetProperty(DeniedToolProperty, out JsonElement toolName)
            && toolName.ValueKind == JsonValueKind.String;
        return hasToolName
            ? denial.GetProperty(DeniedToolProperty).GetString()!
            : denial.GetRawText();
    }
}