using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Steps;
using AgenticWorkflow.Testing;
using AgenticWorkflow.Workflows;

string workspace = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "workspace"));

string inputDirectory = Path.Combine(workspace, "input");
string requirementPath = Path.Combine(inputDirectory, "requirement.md");

Directory.CreateDirectory(inputDirectory);

if (!File.Exists(requirementPath))
{
    await File.WriteAllTextAsync(
        requirementPath,
        """
        # Word Counter

        Build a .NET 10 class library named WordCounter.

        It must expose a public static class `WordCounter.WordCounter` with a public
        static method `CountWords(string input)` returning an integer.

        A word is a maximal contiguous sequence of letters or digits. Punctuation,
        whitespace, and other non-alphanumeric characters separate words and are not
        themselves part of a word.

        Null input is invalid and must throw `ArgumentNullException`.
        Empty or whitespace-only input contains zero words.
        """);
}

using HttpClient httpClient = new();
IAiProvider ai = new ClaudeCodeAiProvider();
IArtifactStore artifactStore = new FileArtifactStore(workspace);
WorkflowContext context = new(artifactStore, workspace);

Artifact requirement = new(
    "requirement",
    "text/markdown",
    "input/requirement.md",
    await File.ReadAllTextAsync(requirementPath));

context.Add(requirement);

IWorkflowStateStore workflowStateStore =
    new FileWorkflowStateStore(
        Path.Combine(workspace, "workflow-state"));

Workflow workflow = new(
    "build-word-counter",
    [
        new GenerateSpecificationStep(ai),

        new ReviewSpecificationStep(ai),

        new ResolveSpecificationStep(ai),

        new ReviewResolvedSpecificationStep(ai),

        new GenerateTestsStep(
            ai,
            new TestGenerationOptions(
                new TestPackageOptions(
                    "visible-tests",
                    "WordCounter.VisibleTests.csproj",
                    "../product/WordCounter.csproj"),
                new TestPackageOptions(
                    "hidden-tests",
                    "WordCounter.HiddenTests.csproj",
                    "../product/WordCounter.csproj"))),

        new ImplementStep(ai),

        new WriteGeneratedFilesStep(
            "generated/source",
            "product"),

        new CommandStep(
            "build-product",
            "dotnet",
            "build product/WordCounter.csproj --nologo",
            workspace),

        new CommandStep(
            "run-visible-tests",
            "dotnet",
            "test visible-tests/WordCounter.VisibleTests.csproj --nologo",
            workspace),

        new CommandStep(
            "run-hidden-tests",
            "dotnet",
            "test hidden-tests/WordCounter.HiddenTests.csproj --nologo",
            workspace),

        new RepairImplementationStep(ai),

        new EvaluateCodeStep(ai)
    ],
    workflowStateStore,
    new WorkflowRetryPolicy(
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["build-product"] = "repair-implementation",
            ["run-visible-tests"] = "repair-implementation",
            ["run-hidden-tests"] = "repair-implementation"
        },
        3));

using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMinutes(5));
WorkflowResult result = await workflow.ExecuteAsync(context, cancellationTokenSource.Token);

Console.WriteLine();
Console.WriteLine($"Workflow: {workflow.Name}");
Console.WriteLine($"Result: {(result.Succeeded ? "PASS" : "FAIL")}");
Console.WriteLine();

foreach (StepExecution execution in result.Steps)
{
    Console.WriteLine($"[{(execution.Result.Succeeded ? "PASS" : "FAIL")}] {execution.StepName}");
    Console.WriteLine(execution.Result.Message);
    Console.WriteLine();
}

return result.Status switch
{
    WorkflowStatus.Succeeded => 0,
    WorkflowStatus.Waiting => 2,
    WorkflowStatus.Failed => 1,
    _ => 1
};
