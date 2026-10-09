using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Formatting;

/// <summary>
/// The port through which the conductor lays out what workers write in the
/// project's own style, so that no worker's success depends on remembering to.
/// </summary>
public interface IFormatting
{
    /// <summary>
    /// Formats the working copy at <paramref name="workingDirectory"/> in place;
    /// fails with <see cref="FormattingFailed"/> and the formatter's report when it cannot.
    /// </summary>
    Task<Fin<Unit>> Format(string workingDirectory, CancellationToken cancellationToken);
}