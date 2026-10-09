using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Gates;

/// <summary>
/// A gate that is a command the project names (in this repository,
/// <c>bash scripts/gates.sh run markdownlint lychee</c>): it passes when the
/// command exits with 0, and its report is everything the command printed.
/// </summary>
/// <remarks>
/// The project decides what "passing" means, so a target project in another
/// language brings its own checks; the conductor only runs them.
/// </remarks>
public sealed class CommandGate(IProcessCapable processes, IReadOnlyList<string> command) : IGateKeeping
{
    private readonly ProjectCommand _command = new(processes, command);

    public Task<Fin<Unit>> Check(string workingDirectory, CancellationToken cancellationToken) =>
        this._command.Run(workingDirectory, report => new GatesFailed(report), cancellationToken);
}