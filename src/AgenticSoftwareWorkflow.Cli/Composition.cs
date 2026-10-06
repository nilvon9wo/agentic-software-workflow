using AgenticSoftwareWorkflow.Conductor.Claude;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.GitHub;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// The composition root: the one place where the real adapters — Claude Code,
/// GitHub, git, operating-system processes, the system clock — are chosen and
/// wired together. Everything else depends only on ports.
/// </summary>
internal sealed class Composition : IConductorComposing
{
    private readonly SystemProcessRunner _processes = new();

    public SpecifyCommand CreateSpecifyCommand(ConductorSettings settings, string repositoryRoot)
    {
        GitHubOptions github = GitHubOptionsFor(settings, repositoryRoot);
        return new SpecifyCommand(
            new GitHubIssues(this._processes, github),
            new ClaudeCodeAgentRunner(this._processes),
            new GitRepository(this._processes, repositoryRoot, settings.CommitAuthor),
            new GitHubPullRequests(this._processes, github, settings.BaseBranch),
            settings.BaseBranch
        );
    }

    public RunLoop CreateRunLoop(ConductorSettings settings, CommandContext context, RunLoopOptions options) =>
        new(
            new GitHubIssues(this._processes, GitHubOptionsFor(settings, context.RepositoryRoot)),
            this.CreateSpecifyCommand(settings, context.RepositoryRoot),
            options,
            context.Output,
            TimeProvider.System
        );

    private static GitHubOptions GitHubOptionsFor(ConductorSettings settings, string repositoryRoot) =>
        new(settings.Repository, settings.Maintainers, repositoryRoot);
}