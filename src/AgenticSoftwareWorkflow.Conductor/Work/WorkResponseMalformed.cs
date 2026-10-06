using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>The work source answered, but its response could not be read.</summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
public sealed record WorkResponseMalformed(string Detail)
    : ExpectedFailure($"The work source's response could not be read: {Detail}");