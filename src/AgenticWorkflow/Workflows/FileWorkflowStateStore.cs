using System.Text.Json;

namespace AgenticWorkflow.Workflows;

public sealed class FileWorkflowStateStore(
    string rootDirectory) : IWorkflowStateStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootDirectory =
        Path.GetFullPath(rootDirectory);

    public async Task<WorkflowExecutionState?> LoadAsync(
        string workflowName,
        CancellationToken cancellationToken)
    {
        string path = this.GetPath(workflowName);

        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(
            path,
            cancellationToken);

        return JsonSerializer.Deserialize<WorkflowExecutionState>(
            json,
            JsonSerializerOptions);
    }

    public async Task SaveAsync(
        WorkflowExecutionState state,
        CancellationToken cancellationToken)
    {
        string path = this.GetPath(state.WorkflowName);

        string? directory = Path.GetDirectoryName(path);

        if (directory is not null)
        {
            _ = Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(
            state,
            JsonSerializerOptions);

        await File.WriteAllTextAsync(
            path,
            json,
            cancellationToken);
    }

    private string GetPath(string workflowName)
    {
        string fileName = workflowName + ".json";

        return Path.Combine(
            this._rootDirectory,
            fileName);
    }
}