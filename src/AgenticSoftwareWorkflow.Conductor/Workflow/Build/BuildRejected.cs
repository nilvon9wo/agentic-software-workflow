using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>
/// The build gave up: the tests or the implementation were still not accepted
/// after every allowed attempt. The findings say what was still wrong.
/// </summary>
[PublicAPI] // the failure's data is its contract with whoever handles it
public sealed record BuildRejected(string Stage, string Findings)
    : ExpectedFailure($"The {Stage} was still not accepted after every allowed attempt:\n{Findings}");