using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>A reviewer's judgement: approve, or revise with the findings that say what to fix.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record ReviewVerdict(string? Verdict, List<string>? Findings) : IStructuredAnswer
{
    private const string Approve = "approve";

    public static string Schema =>
        """
        {
          "type": "object",
          "properties": {
            "verdict": { "enum": ["approve", "revise"] },
            "findings": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["verdict", "findings"],
          "additionalProperties": false
        }
        """;

    public bool IsApproved => this.Verdict == Approve;

    /// <summary>The findings, one per line, for the worker who must address them.</summary>
    public string Describe() => string.Join('\n', (this.Findings ?? []).Select(finding => $"- {finding}"));
}