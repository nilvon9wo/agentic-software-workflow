using System.Text.Json;
using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class ReviewResolvedSpecificationStep(
    IAiProvider ai) : IWorkflowStep
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAiProvider _ai = ai;

    public string Name => "review-resolved-specification";

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        Artifact requirement =
            await context.GetAsync(
                "input/requirement.md",
                cancellationToken);

        Artifact specification =
            await context.GetAsync(
                "generated/specification.json",
                cancellationToken);

        Artifact decision =
            await context.GetAsync(
                "workflow/human-decisions/specification.json",
                cancellationToken);

        AiRequest request = new(
            """
            You are an independent specification reviewer.

            A specification was modified after a human decision.

            Review the resulting specification from scratch.

            Do NOT assume that the refinement agent produced a correct result.

            Review all of the following:

            1. REQUIREMENT FIDELITY

               Verify that the resulting specification still faithfully
               represents the original requirement.

            2. HUMAN DECISION

               Verify that the human decision is actually reflected in the
               normative requirements and acceptance criteria.

            3. INTERNAL CONSISTENCY

               Identify contradictions between requirements, acceptance
               criteria, examples, and definitions.

            4. ACCEPTANCE CRITERIA

               Independently derive the expected result for every concrete
               acceptance criterion.

               Do not trust an expected value merely because it appears in
               the specification.

            5. OPEN QUESTIONS

               OpenQuestions must contain only genuinely unresolved questions.

               A question already resolved by the supplied human decision
               must NOT remain in OpenQuestions.

            6. DOWNSTREAM TESTABILITY

               Determine whether the resulting specification is precise enough
               to generate reliable automated tests and implementation.

            7. REPRESENTATION INTEGRITY

               Look for corrupted, ambiguous, or otherwise damaged literal
               values in examples.

               In particular, report replacement characters such as U+FFFD
               when they appear where a meaningful Unicode character appears
               to have been intended.

            IMPORTANT:

            The specification's normative rules take precedence over
            illustrative examples.

            If an example contradicts a normative rule, report the contradiction.

            Return exactly one valid JSON object and nothing else.

            The JSON object MUST have exactly this structure:

            {
              "findings": [
                {
                  "id": "...",
                  "severity": "ERROR|WARNING",
                  "category": "OMISSION|CONTRADICTION|AMBIGUITY|UNSUPPORTED_ASSUMPTION|TESTABILITY|REPRESENTATION|OTHER",
                  "blocking": true,
                  "description": "...",
                  "evidence": "...",
                  "recommendation": "..."
                }
              ],
              "summary": "..."
            }

            Rules:

            - Each finding must have a stable unique id within this review.
            - ERROR means a substantive problem.
            - WARNING means a minor problem.
            - blocking=true means downstream test generation or implementation
              should not proceed until the finding is resolved.
            - blocking=false means downstream work can safely proceed.
            - Do not invent findings.
            - If the specification is sound, return an empty findings array.
            """,
            $"ORIGINAL REQUIREMENT:\n{requirement.Content}" +
            $"\n\nHUMAN DECISION:\n{decision.Content}" +
            $"\n\nRESOLVED SPECIFICATION:\n{specification.Content}",
            4000);

        AiResponse response =
            await this._ai.CompleteAsync(
                request,
                cancellationToken);

        SpecificationReview review =
            JsonFileParser.Parse<SpecificationReview>(
                response.Text);

        string content =
            JsonSerializer.Serialize(
                review,
                JsonSerializerOptions);

        await context.SaveAsync(
            new Artifact(
                "resolved-specification-review",
                "application/json",
                "quality/resolved-specification-review.json",
                content),
            cancellationToken);

        IReadOnlyList<SpecificationFinding> blockingFindings = [
            .. review.Findings.Where(
                static finding => finding.Blocking)
        ];

        if (blockingFindings.Count == 0)
        {
            return StepResult.Success(
                review.Summary);
        }

        bool decisionExists =
            await context.ArtifactStore.ExistsAsync(
                "workflow/human-decisions/resolved-specification-review.json",
                cancellationToken);

        if (decisionExists)
        {
            return StepResult.Success(
                "Blocking findings have an associated human decision.");
        }

        string findings = string.Join(
            "\n\n",
            blockingFindings.Select(
                static finding =>
                    $"[{finding.Id}] {finding.Severity} {finding.Category}\n" +
                    $"{finding.Description}\n" +
                    $"Evidence: {finding.Evidence}\n" +
                    $"Recommendation: {finding.Recommendation}"));

        return StepResult.Wait(
            "Human decision required before the resolved specification can continue.\n\n" +
            "Blocking findings:\n" +
            findings,
            "resolve-specification");
    }

    private sealed record SpecificationReview(
        IReadOnlyList<SpecificationFinding> Findings,
        string Summary);

    private sealed record SpecificationFinding(
        string Id,
        string Severity,
        string Category,
        bool Blocking,
        string Description,
        string Evidence,
        string Recommendation);
}