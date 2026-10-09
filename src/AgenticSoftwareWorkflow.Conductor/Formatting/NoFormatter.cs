using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Formatting;

/// <summary>
/// The formatter of a project that names none: what workers write is left as
/// they wrote it, and the project's checks alone judge its layout.
/// </summary>
public sealed class NoFormatter : IFormatting
{
    public Task<Fin<Unit>> Format(string workingDirectory, CancellationToken cancellationToken) =>
        Task.FromResult(Fin.Succ(Unit.Default));
}