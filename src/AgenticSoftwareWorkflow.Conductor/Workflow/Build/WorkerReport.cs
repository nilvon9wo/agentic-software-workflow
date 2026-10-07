using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>What a worker that edits files says it did: the test author's or the implementer's summary.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // created by the JSON deserializer
internal sealed record WorkerReport(string? Summary) : IStructuredAnswer
{
    public static string Schema =>
        """
        {
          "type": "object",
          "properties": { "summary": { "type": "string" } },
          "required": ["summary"],
          "additionalProperties": false
        }
        """;

    /// <summary>The summary, empty when the worker gave none.</summary>
    public string Text => this.Summary ?? string.Empty;
}