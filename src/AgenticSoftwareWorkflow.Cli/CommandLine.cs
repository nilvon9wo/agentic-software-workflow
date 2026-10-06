using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// What <c>aswf</c> does with its arguments. Kept apart from <see cref="Program"/>
/// so it can be tested with fake output and a fake command factory.
/// </summary>
internal static class CommandLine
{
    public const int Succeeded = 0;
    public const int Failed = 1;
    public const int UsageError = 64;

    private const string SpecifyVerb = "specify";
    private const string Usage = "Usage: aswf specify <issue-number>";

    public static Task<int> Run(
        IReadOnlyList<string> arguments,
        string repositoryRoot,
        TextWriter output,
        Func<ConductorSettings, string, SpecifyCommand> createSpecifyCommand,
        CancellationToken cancellationToken
    ) =>
        IsSpecify(arguments, out int number)
            ? Specify(number, repositoryRoot, output, createSpecifyCommand, cancellationToken)
            : Report(output, Usage, UsageError);

    private static bool IsSpecify(IReadOnlyList<string> arguments, out int number)
    {
        number = 0;
        return arguments is [SpecifyVerb, _] && int.TryParse(arguments[1], out number);
    }

    private static async Task<int> Specify(
        int number,
        string repositoryRoot,
        TextWriter output,
        Func<ConductorSettings, string, SpecifyCommand> createSpecifyCommand,
        CancellationToken cancellationToken
    )
    {
        Fin<ConductorSettings> settings = ConductorSettings.Load(repositoryRoot);
        Fin<string> report = await settings.Match(
            Succ: loaded => createSpecifyCommand(loaded, repositoryRoot)
                .Run(new WorkItemId($"github:{loaded.Repository}", $"{number}"), cancellationToken),
            Fail: error => Task.FromResult(Fin.Fail<string>(error))
        );
        return await report.Match(
            Succ: message => Report(output, message, Succeeded),
            Fail: error => Report(output, $"Failed ({error.GetType().Name}): {error.Message}", Failed)
        );
    }

    private static async Task<int> Report(TextWriter output, string message, int exitCode)
    {
        await output.WriteLineAsync(message);
        return exitCode;
    }
}