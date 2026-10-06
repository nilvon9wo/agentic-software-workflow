using System.Text.Json;
using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class GenerateSpecificationStep(IAiProvider ai) : IWorkflowStep
{
    private static readonly JsonSerializerOptions JsonSerizlizerOptions = new() { WriteIndented = true };
    private readonly IAiProvider _ai = ai;

    public string Name => "generate-specification";

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        Artifact requirement = await context.GetAsync(
            "input/requirement.md",
            cancellationToken);

        AiRequest request = new(
            """
            You are a requirements analyst. Turn the supplied requirement into a precise,
            implementation-neutral specification. Do not invent requirements. Identify
            assumptions and ambiguities explicitly. Return JSON only with this shape:
            {"summary":"...","requirements":["..."],"acceptanceCriteria":["..."],"openQuestions":["..."]}
            """,
            requirement.Content,
            2500);

        AiResponse response = await this._ai.CompleteAsync(request, cancellationToken);
        Specification specification = JsonFileParser.Parse<Specification>(response.Text);

        string content = System.Text.Json.JsonSerializer.Serialize(
            specification,
            JsonSerizlizerOptions);

        await context.SaveAsync(
            new Artifact(
                "specification",
                "application/json",
                "generated/specification.json",
                content),
            cancellationToken);

        return StepResult.Success(
            $"Specification generated using {response.Model} ({response.InputTokens} input / {response.OutputTokens} output tokens).");
    }

    private sealed record Specification(
        string Summary,
        IReadOnlyList<string> Requirements,
        IReadOnlyList<string> AcceptanceCriteria,
        IReadOnlyList<string> OpenQuestions);
}
