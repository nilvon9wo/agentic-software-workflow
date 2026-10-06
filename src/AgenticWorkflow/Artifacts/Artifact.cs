namespace AgenticWorkflow.Artifacts;

public sealed record Artifact(
    string Name,
    string MediaType,
    string RelativePath,
    string Content);
