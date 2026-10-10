using LanguageExt;
using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>
/// A command the project names in its settings, as an argument list, run in a
/// working copy: it succeeds when the command exits with 0, and otherwise fails
/// with everything the command printed, as the failure its caller chooses.
/// </summary>
/// <remarks>
/// The project decides what its commands do, so a target project in another
/// language brings its own; the conductor only runs them.
/// </remarks>
public sealed class ProjectCommand(IProcessCapable processes, IReadOnlyList<string> command)
{
    private const string NoInput = "";

    // Provisioning the project's tools on a first run can take a while.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    private readonly IProcessCapable _processes = processes;
    private readonly IReadOnlyList<string> _command = command;

    public async Task<Fin<Unit>> Run(
        string workingDirectory,
        Func<string, Error> failure,
        CancellationToken cancellationToken
    )
    {
        ProcessRequest request = new(this._command[0], [.. this._command.Skip(1)], NoInput, workingDirectory, Timeout);
        ProcessOutcome outcome = await this._processes.Run(request, cancellationToken);
        return outcome is { ExitCode: 0, HasTimedOut: false }
            ? Fin.Succ(Unit.Default)
            : Fin.Fail<Unit>(failure(outcome.StandardOutput + outcome.StandardError));
    }
}