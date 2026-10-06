namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// <c>aswf</c>: runs the workflow from a repository's root, where its
/// <c>aswf.json</c> lives.
/// </summary>
/// <remarks>
/// Stopping <c>aswf run</c> (Ctrl+C) ends the process at once. That is safe for
/// the work — its state lives in the work source — but an item stopped mid-way
/// can leave its worktree under <c>.aswf/worktrees/</c>, which then needs
/// removing (<c>git worktree remove</c>) before that item can run again.
/// </remarks>
internal static class Program
{
    public static Task<int> Main(string[] arguments) =>
        CommandLine.Run(
            arguments,
            new CommandContext(Directory.GetCurrentDirectory(), Console.Out, new Composition()),
            CancellationToken.None
        );
}