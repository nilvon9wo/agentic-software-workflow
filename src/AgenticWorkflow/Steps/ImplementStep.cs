using AgenticWorkflow.Ai;
using AgenticWorkflow.Artifacts;
using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class ImplementStep(IAiProvider ai) : IWorkflowStep
{
    private readonly IAiProvider _ai = ai;

    public string Name => "implement";

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        Artifact specification = await context.GetAsync(
            "generated/specification.json",
            cancellationToken);

        IReadOnlyList<Artifact> visibleTests = await ImplementStep.GetArtifactsAsync(
            context,
            "visible-tests",
            cancellationToken);

        string visibleTestText = string.Join(
            "\n\n===== FILE =====\n",
            visibleTests.Select(static artifact =>
                $"{artifact.RelativePath}\n{artifact.Content}"));

        AiRequest request = new(
            """
            You are the implementation engineer. Implement the specification in C#/.NET 10.
            You may use the visible tests as guidance. You must not modify the specification
            or tests and you must not assume anything about hidden tests beyond what follows
            from the specification.

            Return JSON only:
            {"files":[{"path":"...","content":"..."}]}

            Return only implementation project files. Do not return tests. Paths are relative
            to the generated source root, so return WordCounter.csproj and source files such as
            WordCounter/WordCounter.cs, not a leading product/ directory. Keep the solution
            appropriately small and avoid unnecessary abstractions.
            """,
            $"SPECIFICATION:\n{specification.Content}\n\nVISIBLE TESTS:\n{visibleTestText}",
            5000);

        AiResponse response = await this._ai.CompleteAsync(
            request,
            cancellationToken);

        ImplementationPackage package =
            JsonFileParser.Parse<ImplementationPackage>(response.Text);

        string generatedSourceRoot =
            Path.Combine(context.WorkspaceRoot, "generated", "source");

        ImplementStep.DeleteDirectoryIfExists(generatedSourceRoot);

        _ = Directory.CreateDirectory(generatedSourceRoot);

        foreach (GeneratedFile file in package.Files)
        {
            await context.SaveAsync(
                new Artifact(
                    file.Path,
                    GetMediaType(file.Path),
                    "generated/source/" + file.Path,
                    file.Content),
                cancellationToken);
        }

        return StepResult.Success(
            $"Generated {package.Files.Count} implementation files using {response.Model}.");
    }

    private static string GetMediaType(string path)
        => Path.GetExtension(path).Equals(
            ".csproj",
            StringComparison.OrdinalIgnoreCase)
            ? "application/xml"
            : "text/x-csharp";

    private static async Task<IReadOnlyList<Artifact>> GetArtifactsAsync(
        WorkflowContext context,
        string prefix,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(context.WorkspaceRoot, prefix);

        if (!Directory.Exists(root))
        {
            return [];
        }

        List<Artifact> artifacts = [];

        foreach (string file in Directory.EnumerateFiles(
                     root,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file);
            string content = await File.ReadAllTextAsync(
                file,
                cancellationToken);

            artifacts.Add(
                new Artifact(
                    relative,
                    "text/plain",
                    prefix + "/" + relative,
                    content));
        }

        return artifacts;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private sealed record ImplementationPackage(
        IReadOnlyList<GeneratedFile> Files);

    private sealed record GeneratedFile(
        string Path,
        string Content);
}
