using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// The specifier's structured answer, as constrained by <see cref="Schema"/>:
/// either a specification or the questions that must be answered first.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record SpecifierAnswer(string? Outcome, string? Specification, List<string>? Questions)
{
    public const string SpecifiedOutcome = "specified";
    public const string QuestionsOutcome = "questions";

    public const string Schema = """
        {
          "type": "object",
          "properties": {
            "outcome": { "enum": ["specified", "questions"] },
            "specification": { "type": "string" },
            "questions": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["outcome"],
          "additionalProperties": false
        }
        """;
}