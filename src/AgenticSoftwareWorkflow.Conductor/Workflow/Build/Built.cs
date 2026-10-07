namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>The build succeeded: reviewed tests, and an implementation that passed the checks and review.</summary>
/// <param name="TestsSummary">What the test author said it wrote.</param>
/// <param name="ImplementationSummary">What the implementer said it changed.</param>
public sealed record Built(string TestsSummary, string ImplementationSummary);