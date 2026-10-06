using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Processes;

/// <summary>
/// A command-line tool (git, gh) run from one directory: its standard output
/// when it succeeds, or a <see cref="CommandErrors"/> failure naming the
/// command and what it printed when it does not.
/// </summary>
public sealed class CommandLineTool(IProcessCapable processes, string executable, string workingDirectory)
{
    private const int TimedOutExitCode = -1;
    private const string TimedOutMessage = "timed out";
    private const int SubcommandWords = 2;

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly IProcessCapable _processes = processes;
    private readonly string _executable = executable;
    private readonly string _workingDirectory = workingDirectory;

    public Task<Fin<string>> Run(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        this.Run(arguments, string.Empty, cancellationToken);

    public async Task<Fin<string>> Run(
        IReadOnlyList<string> arguments,
        string standardInput,
        CancellationToken cancellationToken
    )
    {
        ProcessRequest request = new(this._executable, arguments, standardInput, this._workingDirectory, Timeout);
        ProcessOutcome outcome = await this._processes.Run(request, cancellationToken);
        string command = this.Describe(arguments);
        return ToOutput(outcome, command);
    }

    private static Fin<string> ToOutput(ProcessOutcome outcome, string command) =>
        outcome switch
        {
            { HasTimedOut: true } => Fin.Fail<string>(
                CommandErrors.Failed(command, TimedOutExitCode, TimedOutMessage)
            ),
            { ExitCode: 0 } => Fin.Succ(outcome.StandardOutput),
            _ => Fin.Fail<string>(CommandErrors.Failed(command, outcome.ExitCode, outcome.StandardError)),
        };

    /// <summary>
    /// The tool and its subcommand (<c>gh issue list</c>, <c>git worktree add</c>):
    /// enough to identify the step, without echoing arguments that may be long
    /// or sensitive.
    /// </summary>
    private string Describe(IReadOnlyList<string> arguments)
    {
        IEnumerable<string> subcommand = arguments.Take(SubcommandWords);
        return string.Join(' ', [this._executable, .. subcommand]);
    }
}