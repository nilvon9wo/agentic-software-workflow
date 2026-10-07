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
    private const string NoInput = "";

    // Provisioning the checkers on a first run can take a while.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    private readonly IProcessCapable _processes = processes;
    private readonly IReadOnlyList<string> _command = command;

    public async Task<Fin<Unit>> Check(string workingDirectory, CancellationToken cancellationToken)
    {
        ProcessRequest request = new(this._command[0], [.. this._command.Skip(1)], NoInput, workingDirectory, Timeout);
        ProcessOutcome outcome = await this._processes.Run(request, cancellationToken);
        return outcome is { ExitCode: 0, HasTimedOut: false }
            ? Fin.Succ(Unit.Default)
            : Fin.Fail<Unit>(new GatesFailed(outcome.StandardOutput + outcome.StandardError));
    }
}