using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Formatting;

/// <summary>The project's formatter could not format what was written; the report says why.</summary>
[PublicAPI] // the failure's data is its contract with whoever handles it
public sealed record FormattingFailed(string Report)
    : ExpectedFailure($"The project's formatter failed:\n{Report}");