namespace AgenticSoftwareWorkflow.Cli;

/// <summary>Where a command runs, where it reports, and what builds it.</summary>
/// <param name="RepositoryRoot">The repository's root, where <c>aswf.json</c> lives.</param>
/// <param name="Output">Where the command reports what happened.</param>
/// <param name="Composer">What builds the command from the repository's settings.</param>
internal sealed record CommandContext(string RepositoryRoot, TextWriter Output, IConductorComposing Composer);