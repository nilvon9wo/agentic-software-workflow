using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// The <c>rate_limit_info</c> of a <c>rate_limit_event</c> message: whether the
/// account may still run (<c>allowed</c>, <c>allowed_warning</c>) or not
/// (<c>rejected</c>), which limit applies, and when it resets (Unix seconds).
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record ClaudeRateLimitInfo(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("rateLimitType")] string? RateLimitType,
    [property: JsonPropertyName("resetsAt")] long? ResetsAt
);