using AgenticWorkflow.Artifacts;

namespace AgenticWorkflow.Workflows;

public sealed class WorkflowContext(IArtifactStore artifactStore, string workspaceRoot)
{
    private readonly Dictionary<string, Artifact> _artifacts = new(StringComparer.OrdinalIgnoreCase);

    public IArtifactStore ArtifactStore { get; } = artifactStore;

    public string WorkspaceRoot { get; } = Path.GetFullPath(workspaceRoot);

    public void Add(Artifact artifact) => this._artifacts[artifact.RelativePath] = artifact;

    public bool TryGet(string relativePath, out Artifact? artifact) => this._artifacts.TryGetValue(relativePath, out artifact);

    public async Task<Artifact> GetAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (this._artifacts.TryGetValue(relativePath, out Artifact? artifact))
        {
            return artifact;
        }

        Artifact loaded = await this.ArtifactStore.GetAsync(relativePath, cancellationToken);
        this._artifacts[relativePath] = loaded;
        return loaded;
    }

    public async Task SaveAsync(Artifact artifact, CancellationToken cancellationToken)
    {
        await this.ArtifactStore.SaveAsync(artifact, cancellationToken);
        this._artifacts[artifact.RelativePath] = artifact;
    }
}
