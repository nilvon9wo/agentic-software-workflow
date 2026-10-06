using AgenticSoftwareWorkflow.Conductor.Claude;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.GitHub;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// The composition root: the one place where the real adapters — Claude Code,
/// GitHub, git, operating-system processes — are chosen and wired together.
/// Everything else depends only on ports.
/// </summary>
internal static class Composition
{
    public static SpecifyCommand CreateSpecifyCommand(ConductorSettings settings, string repositoryRoot)
    {
        SystemProcessRunner processes = new();
        GitHubOptions github = new(settings.Repository, settings.Maintainers, repositoryRoot);
        return new SpecifyCommand(
            new GitHubIssues(processes, github),
            new ClaudeCodeAgentRunner(processes),
            new GitRepository(processes, repositoryRoot, settings.CommitAuthor),
            new GitHubPullRequests(processes, github, settings.BaseBranch),
            settings.BaseBranch
        );
    }
}