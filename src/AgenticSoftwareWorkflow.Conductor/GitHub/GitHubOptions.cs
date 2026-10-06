namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// Where the work lives and who may answer for it.
/// </summary>
/// <param name="Repository">The repository, as <c>owner/name</c>.</param>
/// <param name="Maintainers">
/// The GitHub logins whose comments are trusted, and to whom questions are
/// assigned. Membership is by explicit login, never by GitHub's own
/// "collaborator" association: the workers' bot account is a collaborator
/// too, and its own questions must never be mistaken for answers.
/// </param>
/// <param name="WorkingDirectory">Where to run <c>gh</c>.</param>
/// <param name="Executable">The GitHub CLI, signed in as the workers' account.</param>
public sealed record GitHubOptions(
    string Repository,
    IReadOnlyList<string> Maintainers,
    string WorkingDirectory,
    string Executable = "gh"
);