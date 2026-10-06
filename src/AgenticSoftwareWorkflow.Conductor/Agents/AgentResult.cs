using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// A finished run: its answer, the structured answer when a schema was given,
/// what it consumed, and every tool call that was refused.
/// </summary>
/// <param name="DeniedTools">
/// Tools the run tried to use but was not permitted. A non-empty list means a
/// role reached beyond its authority: the run still finished, but the attempt is
/// worth surfacing, never ignoring.
/// </param>
public sealed record AgentResult(
    string Text,
    Option<string> StructuredOutputJson,
    AgentUsage Usage,
    IReadOnlyList<string> DeniedTools
);