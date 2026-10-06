using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// Turns how a `claude -p` process ended into the agent's result, or into the
/// failure (<see cref="AgentTimedOut"/>, <see cref="UsageLimitReached"/>,
/// <see cref="AgentProcessFailed"/>, …) that explains why there is none.
/// </summary>
internal static class ClaudeOutputReader
{
    private const string DeniedToolProperty = "tool_name";
    private const string UnknownErrorKind = "unknown";

    public static Fin<AgentResult> Read(ProcessOutcome outcome, TimeSpan timeout) =>
        outcome.HasTimedOut
            ? Fin.Fail<AgentResult>(new AgentTimedOut(timeout))
            : ReadMessages(outcome);

    // A rejected usage limit explains the run's end whatever else it printed,
    // so it is checked before the result and the exit code.
    private static Fin<AgentResult> ReadMessages(ProcessOutcome outcome)
    {
        Fin<IReadOnlyList<JsonElement>> messages = ClaudeMessages.Parse(outcome.StandardOutput);
        Option<UsageLimitReached> limit = messages.Match(
            Succ: ClaudeMessages.RejectedLimit,
            Fail: _ => Option<UsageLimitReached>.None
        );
        return limit.Match(
            Some: Fin.Fail<AgentResult>,
            None: () => ReadEnvelope(outcome, messages).Bind(ToResult)
        );
    }

    private static Fin<ClaudeEnvelope> ReadEnvelope(ProcessOutcome outcome, Fin<IReadOnlyList<JsonElement>> messages)
    {
        Fin<ClaudeEnvelope> envelope = messages.Bind(ClaudeMessages.Result);
        return outcome.ExitCode == 0
            ? envelope
            : envelope.MapFail(_ => new AgentProcessFailed(outcome.ExitCode, outcome.StandardError));
    }

    private static Fin<AgentResult> ToResult(ClaudeEnvelope envelope) =>
        envelope.IsError
            ? Fin.Fail<AgentResult>(ReportedError(envelope))
            : Fin.Succ(Describe(envelope));

    private static AgentReportedError ReportedError(ClaudeEnvelope envelope) =>
        new(envelope.Subtype ?? UnknownErrorKind, envelope.Result ?? string.Empty);

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