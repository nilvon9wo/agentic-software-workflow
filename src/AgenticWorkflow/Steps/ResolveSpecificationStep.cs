using System.Text.Json;
using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class ResolveSpecificationStep(
    IAiProvider ai) : IWorkflowStep
{
    private readonly IAiProvider _ai = ai;

    public string Name => "resolve-specification";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        Artifact specification =
            await context.GetAsync(
                "generated/specification.json",
                cancellationToken);

        Artifact review =
            await context.GetAsync(
                "quality/specification-review.json",
                cancellationToken);

        string decisionPath;

        bool resolvedSpecificationDecisionExists =
            await context.ArtifactStore.ExistsAsync(
                "workflow/human-decisions/resolved-specification-review.json",
                cancellationToken);

        decisionPath = resolvedSpecificationDecisionExists
            ? "workflow/human-decisions/resolved-specification-review.json"
            : "workflow/human-decisions/specification.json";

        Artifact decision =
            await context.GetAsync(
                decisionPath,
                cancellationToken);

        AiRequest request = new(
            """
            You are a specification refinement agent.

            A specification was reviewed and a human has supplied an
            authoritative decision resolving one or more blocking findings.

            The human decision may resolve an original specification ambiguity or
            correct a defect discovered during a later independent specification review.

            Apply the decision to the current specification, not to an obsolete
            earlier version.

            Update the specification so that the human decision is explicitly
            reflected in the normative requirements and acceptance criteria.

            Preserve all requirements that are unaffected by the decision.

            Do not invent unrelated requirements.

            IMPORTANT OPEN-QUESTION RULE:

            OpenQuestions must contain ONLY questions that are still unresolved
            and could affect downstream work.

            If a human decision resolves an OpenQuestion, REMOVE that question
            from OpenQuestions completely.

            Do NOT leave resolved questions in OpenQuestions with labels such
            as "RESOLVED", "ANSWERED", or "CLOSED".

            Historical decisions belong in the workflow decision artifacts,
            not in OpenQuestions.

            IMPORTANT CONSISTENCY RULE:

            Independently verify every concrete example and acceptance
            criterion against the normative requirements.

            Do not blindly preserve an existing expected value if it conflicts
            with the normative definition.

            If an existing example or acceptance criterion is inconsistent
            with the normative rules, correct it.

            Do not introduce a contradiction while resolving another issue.

            Return exactly one valid JSON object and nothing else.

            Preserve this exact structure:

            {
              "Summary": "...",
              "Requirements": ["..."],
              "AcceptanceCriteria": ["..."],
              "OpenQuestions": ["..."]
            }

            Before returning the response verify:

            - the human decision is reflected normatively;
            - resolved questions have been removed from OpenQuestions;
            - no resolved question remains in OpenQuestions under another
              wording;
            - every acceptance criterion follows from the requirements;
            - every concrete expected result is independently correct;
            - no unrelated requirement was invented;
            - the JSON is valid.
            """,
            $"SPECIFICATION:\n{specification.Content}" +
            $"\n\nSPECIFICATION REVIEW:\n{review.Content}" +
            $"\n\nHUMAN DECISION:\n{decision.Content}",
            4000);

        AiResponse response =
            await this._ai.CompleteAsync(
                request,
                cancellationToken);

        SpecificationDocument refined =
            JsonFileParser.Parse<SpecificationDocument>(
                response.Text);

        string content =
            System.Text.Json.JsonSerializer.Serialize(
                refined,
                JsonSerializerOptions);

        await context.SaveAsync(
            new Artifact(
                "specification",
                "application/json",
                "generated/specification.json",
                content),
            cancellationToken);

        return StepResult.Success(
            $"Specification refined using human decision with {response.Model}.");
    }

    private sealed record SpecificationDocument(
        string Summary,
        IReadOnlyList<string> Requirements,
        IReadOnlyList<string> AcceptanceCriteria,
        IReadOnlyList<string> OpenQuestions);
}