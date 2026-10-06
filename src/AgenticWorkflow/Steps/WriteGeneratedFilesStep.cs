using AgenticWorkflow.Workflows;

namespace AgenticWorkflow.Steps;

public sealed class WriteGeneratedFilesStep(
    string artifactPrefix,
    string targetDirectory) : IWorkflowStep
{
    public string ArtifactPrefix { get; } = artifactPrefix;

    public string TargetDirectory { get; } = targetDirectory;

    public string Name
        => "materialize-" + this.ArtifactPrefix.Replace('/', '-');

    public async Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        string sourceRoot =
            Path.Combine(context.WorkspaceRoot, this.ArtifactPrefix);

        string targetRoot =
            Path.Combine(context.WorkspaceRoot, this.TargetDirectory);

        if (!Directory.Exists(sourceRoot))
        {
            return StepResult.Failure(
                $"No generated files exist under '{this.ArtifactPrefix}'.");
        }

        if (Directory.Exists(targetRoot))
        {
            Directory.Delete(targetRoot, true);
        }

        _ = Directory.CreateDirectory(targetRoot);

        foreach (string sourceFile in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relative =
                Path.GetRelativePath(sourceRoot, sourceFile);

            string targetFile =
                Path.Combine(targetRoot, relative);

            string? directory =
                Path.GetDirectoryName(targetFile);

            if (directory is not null)
            {
                _ = Directory.CreateDirectory(directory);
            }

            File.Copy(sourceFile, targetFile, true);
        }

        await Task.CompletedTask;

        return StepResult.Success(
            $"Materialized generated files into '{this.TargetDirectory}'.");
    }
}
