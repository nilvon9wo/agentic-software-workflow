namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// <c>aswf</c>: runs the workflow from a repository's root, where its
/// <c>aswf.json</c> lives.
/// </summary>
internal static class Program
{
    public static Task<int> Main(string[] arguments) =>
        CommandLine.Run(
            arguments,
            Directory.GetCurrentDirectory(),
            Console.Out,
            Composition.CreateSpecifyCommand,
            CancellationToken.None
        );
}