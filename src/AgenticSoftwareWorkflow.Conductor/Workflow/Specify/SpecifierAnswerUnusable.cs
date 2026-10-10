using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// The specifier answered, but not in a form the stage can act on: no
/// structured output, an outcome without its content, or unreadable JSON.
/// </summary>
public sealed record SpecifierAnswerUnusable : ExpectedFailure
{
    public SpecifierAnswerUnusable(string detail)
        : base($"The specifier's answer cannot be used: {detail}")
    {
    }
}