namespace AgenticWorkflow.Artifacts;

public interface IArtifactStore
{
    Task SaveAsync(Artifact artifact, CancellationToken cancellationToken);

    Task<Artifact> GetAsync(string relativePath, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken);
}
