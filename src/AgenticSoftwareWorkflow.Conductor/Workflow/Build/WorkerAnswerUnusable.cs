using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>A worker answered, but not in a form the stage can act on.</summary>
[PublicAPI] // the failure's data is its contract with whoever handles it
public sealed record WorkerAnswerUnusable(string Detail)
    : ExpectedFailure($"A worker's answer cannot be used: {Detail}");