namespace AgenticWorkflow.Testing;

public sealed record TestGenerationOptions(
    TestPackageOptions Visible,
    TestPackageOptions Hidden);

public sealed record TestPackageOptions(
    string ArtifactPrefix,
    string ProjectFileName,
    string ProductProjectReference);