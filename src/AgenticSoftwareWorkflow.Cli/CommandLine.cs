using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// What <c>aswf</c> does with its arguments. Kept apart from <see cref="Program"/>
/// so it can be tested with fake output and fake commands.
/// </summary>
internal static class CommandLine
{
    public const int Succeeded = 0;
    public const int Failed = 1;
    public const int UsageError = 64;

    private const string SpecifyVerb = "specify";
    private const string BuildVerb = "build";
    private const string RunVerb = "run";
    private const string OnceFlag = "--once";
    private const string PassComplete = "The pass is complete.";

    private const string Usage = """
        Usage: aswf specify <issue-number>   specify one issue
               aswf build <issue-number>     build one issue from its approved specification
               aswf run [--once]             work through the ready issues, then keep watching (or stop)
        """;

    public static Task<int> Run(
        IReadOnlyList<string> arguments,
        CommandContext context,
        CancellationToken cancellationToken
    ) =>
        arguments switch
        {
            [SpecifyVerb, { } number] when int.TryParse(number, out int issue) =>
                Specify(issue, context, cancellationToken),
            [BuildVerb, { } number] when int.TryParse(number, out int issue) =>
                Build(issue, context, cancellationToken),
            [RunVerb] => RunLoop(RunLoopOptions.Continuous, context, cancellationToken),
            [RunVerb, OnceFlag] => RunLoop(RunLoopOptions.Once, context, cancellationToken),
            _ => Report(context.Output, Usage, UsageError),
        };

    private static Task<int> Specify(int issue, CommandContext context, CancellationToken cancellationToken) =>
        WithSettings(
            context,
            settings => context.Composer
                .CreateSpecifyCommand(settings, context.RepositoryRoot)
                .Run(new WorkItemId($"github:{settings.Repository}", $"{issue}"), cancellationToken)
        );

    private static Task<int> Build(int issue, CommandContext context, CancellationToken cancellationToken) =>
        WithSettings(
            context,
            settings => context.Composer
                .CreatePipeline(settings, context.RepositoryRoot)
                .Build(new WorkItemId($"github:{settings.Repository}", $"{issue}"), cancellationToken)
        );

    private static Task<int> RunLoop(
        RunLoopOptions options,
        CommandContext context,
        CancellationToken cancellationToken
    ) =>
        WithSettings(
            context,
            async settings =>
            {
                await context.Composer
                    .CreateRunLoop(settings, context, options)
                    .Run(cancellationToken);
                return Fin.Succ(PassComplete);
            }
        );

    private static async Task<int> WithSettings(
        CommandContext context,
        Func<ConductorSettings, Task<Fin<string>>> command
    )
    {
        Fin<ConductorSettings> settings = ConductorSettings.Load(context.RepositoryRoot);
        Fin<string> report = await settings.Match(
            Succ: command,
            Fail: error => Task.FromResult(Fin.Fail<string>(error))
        );
        return await report.Match(
            Succ: message => Report(context.Output, message, Succeeded),
            Fail: error => Report(context.Output, $"Failed ({error.GetType().Name}): {error.Message}", Failed)
        );
    }

    private static async Task<int> Report(TextWriter output, string message, int exitCode)
    {
        await output.WriteLineAsync(message);
        return exitCode;
    }
}