using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// The JSON document `claude -p --output-format json` prints when it finishes.
/// Only the fields the conductor uses are read; the rest are ignored.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record ClaudeEnvelope(
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("is_error")] bool IsError,
    [property: JsonPropertyName("subtype")] string? Subtype,
    [property: JsonPropertyName("structured_output")] JsonElement? StructuredOutput,
    [property: JsonPropertyName("total_cost_usd")] decimal TotalCostUsd,
    [property: JsonPropertyName("num_turns")] int NumberOfTurns,
    [property: JsonPropertyName("usage")] ClaudeUsage? Usage,
    [property: JsonPropertyName("modelUsage")] Dictionary<string, JsonElement>? ModelUsage,
    [property: JsonPropertyName("permission_denials")] List<JsonElement>? PermissionDenials
);