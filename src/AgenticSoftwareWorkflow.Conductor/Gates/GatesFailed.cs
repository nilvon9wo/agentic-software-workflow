using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Gates;

/// <summary>The project's checks rejected what was written; the report says why.</summary>
[PublicAPI] // the failure's data is its contract with whoever handles it
public sealed record GatesFailed(string Report)
    : ExpectedFailure($"The project's checks failed:\n{Report}");