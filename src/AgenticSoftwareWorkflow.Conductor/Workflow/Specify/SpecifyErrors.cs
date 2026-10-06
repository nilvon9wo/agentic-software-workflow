using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>The ways the specify stage itself can fail, each with a stable code.</summary>
public static class SpecifyErrors
{
    public const int UnusableAnswerCode = 3001;

    /// <summary>
    /// The specifier answered, but not in a form the stage can act on: no
    /// structured output, an outcome without its content, or unreadable JSON.
    /// </summary>
    public static Error UnusableAnswer(string detail) =>
        Error.New(UnusableAnswerCode, $"The specifier's answer cannot be used: {detail}");
}