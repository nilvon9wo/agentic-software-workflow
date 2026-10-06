using System.Text.Json.Serialization;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>Token counts for a whole `claude -p` run.</summary>
internal sealed record ClaudeUsage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens
);