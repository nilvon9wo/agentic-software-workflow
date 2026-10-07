using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>The project names no checks for code, so nothing may be built: building unchecked is refused.</summary>
[PublicAPI] // the failure's data is its contract with whoever handles it
public sealed record BuildingNotConfigured()
    : ExpectedFailure("This project names no codeGate (the command that checks code), so nothing is built.");