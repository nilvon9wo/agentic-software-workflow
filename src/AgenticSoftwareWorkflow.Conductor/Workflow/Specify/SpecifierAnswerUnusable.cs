using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// The specifier answered, but not in a form the stage can act on: no
/// structured output, an outcome without its content, or unreadable JSON.
/// </summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
public sealed record SpecifierAnswerUnusable(string Detail)
    : ExpectedFailure($"The specifier's answer cannot be used: {Detail}");