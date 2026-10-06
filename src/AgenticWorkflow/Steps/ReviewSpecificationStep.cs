using System.Text.Json;
using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class ReviewSpecificationStep(IAiProvider ai) : IWorkflowStep
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAiProvider _ai = ai;

    public string Name => "review-specification";

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        Artifact requirement = await context.GetAsync(
            "input/requirement.md",
            cancellationToken);

        Artifact specification = await context.GetAsync(
            "generated/specification.json",
            cancellationToken);

        AiRequest request = new(
            """
            You are an independent specification reviewer in an agentic
            software-development workflow.

            Review the proposed specification against the original requirement.

            Do not modify the specification. Do not propose a replacement
            specification. Your task is to identify problems that should be
            resolved before downstream test and implementation work begins.

            Review for all of the following:

            1. REQUIREMENT COVERAGE
               Determine whether the specification faithfully represents the
               original requirement. Identify requirements that were omitted,
               materially changed, weakened, strengthened, or invented.

            2. CONTRADICTIONS
               Look for contradictions within the specification and between
               the original requirement and the specification.

            3. ACCEPTANCE CRITERIA
               Check whether every acceptance criterion actually follows from
               the stated requirements and definitions.

            4. CONCRETE EXAMPLES
               Independently reason through every concrete example and its
               expected result. Do not assume that an expected result is
               correct merely because it appears in the specification.

               If a concrete example conflicts with a normative rule, report
               the conflict rather than treating the example as authoritative.

            5. AMBIGUITIES
               Identify unresolved questions that could materially affect
               downstream behavior.

               An ambiguity is BLOCKING when resolving it could change any of:
               - observable behavior;
               - acceptance criteria;
               - public API;
               - compatibility;
               - data interpretation;
               - implementation strategy;
               - test expectations.

               An ambiguity is NON-BLOCKING when downstream work can proceed
               without making a consequential assumption.

               Do not classify every item in an OpenQuestions list as
               automatically blocking. Judge its actual impact.

            6. UNSUPPORTED ASSUMPTIONS
               Identify decisions presented as settled by the specification
               that are not justified by the original requirement.

            7. TESTABILITY
               Determine whether the requirements and acceptance criteria are
               sufficiently precise to derive meaningful automated tests.

            IMPORTANT:

            Distinguish normative rules from incidental wording.

            If a concrete example conflicts with a normative rule, report the
            conflict.

            Return exactly one valid JSON object and nothing else.

            The JSON object MUST have exactly this structure:

            {
              "findings": [
                {
                  "severity": "ERROR|WARNING",
                  "category": "OMISSION|CONTRADICTION|AMBIGUITY|UNSUPPORTED_ASSUMPTION|TESTABILITY|OTHER",
                  "blocking": true,
                  "description": "...",
                  "evidence": "...",
                  "recommendation": "..."
                }
              ],
              "summary": "..."
            }

            Rules for findings:

            - Use severity ERROR for a substantive problem that should prevent
              downstream work.
            - Use severity WARNING for a minor issue that does not prevent
              downstream work.
            - Set blocking to true when resolving the finding is necessary
              before downstream test generation or implementation can proceed
              reliably.
            - Set blocking to false when downstream work can safely proceed
              without resolving the finding.
            - Do not create findings merely because an OpenQuestions array
              exists.
            - Do not invent problems that are not supported by the supplied
              requirement or specification.

            If the specification is materially sound and has no blocking
            findings, return an empty findings array.
            """,
            $"ORIGINAL REQUIREMENT:\n{requirement.Content}" +
            $"\n\nPROPOSED SPECIFICATION:\n{specification.Content}",
            4000);

        AiResponse response = await this._ai.CompleteAsync(
            request,
            cancellationToken);

        SpecificationReview review =
            JsonFileParser.Parse<SpecificationReview>(response.Text);

        string content = JsonSerializer.Serialize(
            review,
            JsonSerializerOptions);

        await context.SaveAsync(
            new Artifact(
                "specification-review",
                "application/json",
                "quality/specification-review.json",
                content),
            cancellationToken);

        IReadOnlyList<SpecificationFinding> blockingFindings = [
            .. review.Findings.Where(
                static finding => finding.Blocking)
        ];

        if (blockingFindings.Count == 0)
        {
            return StepResult.Success(review.Summary);
        }

        bool decisionExists =
            await context.ArtifactStore.ExistsAsync(
                "workflow/human-decisions/specification.json",
                cancellationToken);

        if (decisionExists)
        {
            return StepResult.Success(
                "Blocking specification findings have an associated human decision.");
        }

        string findings = string.Join(
            "\n\n",
            blockingFindings.Select(
                static finding =>
                    $"[{finding.Severity}] {finding.Category}\n" +
                    $"{finding.Description}\n" +
                    $"Evidence: {finding.Evidence}\n" +
                    $"Recommendation: {finding.Recommendation}"));

        return StepResult.Wait(
            "Human decision required before specification work can continue.\n\n" +
            "Blocking specification findings:\n" +
            findings,
            "resolve-specification");
    }

    private sealed record SpecificationReview(
        IReadOnlyList<SpecificationFinding> Findings,
        string Summary);

    private sealed record SpecificationFinding(
        string Severity,
        string Category,
        bool Blocking,
        string Description,
        string Evidence,
        string Recommendation);
}