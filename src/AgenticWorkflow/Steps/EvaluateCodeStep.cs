using System.Text.Json;
using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Evaluation;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class EvaluateCodeStep(IAiProvider ai) : IWorkflowStep
{
    private static readonly JsonSerializerOptions JsonSerializeroptions = new() { WriteIndented = true };
    private readonly IAiProvider _ai = ai;

    public string Name => "evaluate-code";

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        Artifact specification = await context.GetAsync(
            "generated/specification.json",
            cancellationToken);

        string sourceRoot = Path.Combine(context.WorkspaceRoot, "generated", "source");
        string source = ReadFiles(sourceRoot);
        string testRoot = Path.Combine(context.WorkspaceRoot, "visible-tests");
        string tests = ReadFiles(testRoot);

        AiRequest request = new(
            """
            You are a read-only code evaluator. Do not propose edits to the files as part of
            this task. Judge whether the implementation is consistent with the specification
            and whether it is reasonably maintainable. Consider naming, unnecessary complexity,
            SOLID/YAGNI/DRY, comments, method/class size, and obvious design problems.
            Return JSON only:
            {"verdict":"PASS|FAIL","findings":["..."],"strengths":["..."],"summary":"..."}
            """,
            $"SPECIFICATION:\n{specification.Content}\n\nIMPLEMENTATION:\n{source}\n\nVISIBLE TESTS:\n{tests}",
            3000);

        AiResponse response = await this._ai.CompleteAsync(request, cancellationToken);
        CodeEvaluation evaluation = JsonFileParser.Parse<CodeEvaluation>(response.Text);
        string content = System.Text.Json.JsonSerializer.Serialize(
            evaluation,
            JsonSerializeroptions);

        await context.SaveAsync(
            new Artifact(
                "code-evaluation",
                "application/json",
                "quality/code-evaluation.json",
                content),
            cancellationToken);

        return evaluation.Verdict.Equals("PASS", StringComparison.OrdinalIgnoreCase)
            ? StepResult.Success(evaluation.Summary)
            : StepResult.Failure(evaluation.Summary);
    }

    private static string ReadFiles(string root)
    {
        if (!Directory.Exists(root))
        {
            return "<none>";
        }

        string[] allowedExtensions =
        [
            ".cs",
        ".csproj"
        ];

        string[] files = [.. Directory.EnumerateFiles(
                root,
                "*",
                SearchOption.AllDirectories)
            .Where(
                file => allowedExtensions.Contains(
                    Path.GetExtension(file),
                    StringComparer.OrdinalIgnoreCase))
            .Where(
                file => !file.Split(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                    .Any(
                        part => part.Equals(
                            "bin",
                            StringComparison.OrdinalIgnoreCase) ||
                            part.Equals(
                            "obj",
                            StringComparison.OrdinalIgnoreCase)))
            .OrderBy(
                file => file,
                StringComparer.OrdinalIgnoreCase)];

        return files.Length == 0
            ? "<none>"
            : string.Join(
            "\n\n===== FILE =====\n",
            files.Select(
                file =>
                    $"{Path.GetRelativePath(root, file)}\n" +
                    File.ReadAllText(file)));
    }
}
