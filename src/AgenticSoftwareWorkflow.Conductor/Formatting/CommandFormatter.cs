using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Formatting;

/// <summary>
/// A formatter that is a command the project names (in this repository,
/// <c>dotnet restore</c>, so a package error is named, then <c>dotnet format</c>): it
/// rewrites the working copy, and succeeds when the command exits with 0.
/// </summary>
public sealed class CommandFormatter(IProcessCapable processes, IReadOnlyList<string> command) : IFormatting
{
    private readonly ProjectCommand _command = new(processes, command);

    public Task<Fin<Unit>> Format(string workingDirectory, CancellationToken cancellationToken) =>
        this._command.Run(workingDirectory, report => new FormattingFailed(report), cancellationToken);
}