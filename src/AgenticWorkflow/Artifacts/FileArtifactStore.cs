namespace AgenticWorkflow.Artifacts;

public sealed class FileArtifactStore : IArtifactStore
{
    private readonly string _rootDirectory;

    public FileArtifactStore(string rootDirectory)
    {
        this._rootDirectory = Path.GetFullPath(rootDirectory);
        _ = Directory.CreateDirectory(this._rootDirectory);
    }

    public async Task SaveAsync(Artifact artifact, CancellationToken cancellationToken)
    {
        string path = this.GetSafePath(artifact.RelativePath);
        string? directory = Path.GetDirectoryName(path);

        if (directory is not null)
        {
            _ = Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, artifact.Content, cancellationToken);
    }

    public async Task<Artifact> GetAsync(string relativePath, CancellationToken cancellationToken)
    {
        string path = this.GetSafePath(relativePath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Artifact '{relativePath}' does not exist.", path);
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        string name = Path.GetFileName(path);

        return new Artifact(name, FileArtifactStore.GetMediaType(path), relativePath, content);
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(this.GetSafePath(relativePath)));
    }

    private string GetSafePath(string relativePath)
    {
        string combined = Path.GetFullPath(Path.Combine(this._rootDirectory, relativePath));
        string root = this._rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? this._rootDirectory
            : this._rootDirectory + Path.DirectorySeparatorChar;

        return !combined.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? throw new InvalidOperationException($"Artifact path escapes the artifact store: '{relativePath}'.")
            : combined;
    }

    private static string GetMediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".md" => "text/markdown",
        ".json" => "application/json",
        ".cs" => "text/x-csharp",
        ".csproj" => "application/xml",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}
