using AgenticSoftwareWorkflow.Conductor.Claude;
using AgenticSoftwareWorkflow.Conductor.Gates;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.GitHub;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Build;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

using LanguageExt;

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
        GitHubIssues issues = new(this._processes, github);
        SpecifyStage stage = new(
            issues,
            new ClaudeCodeAgentRunner(this._processes),
            // Never null here: ConductorSettings.Load refuses settings without a documentGate.
            new CommandGate(this._processes, settings.DocumentGate!)
        );
        return new SpecifyCommand(
            stage,
            issues,
            new GitRepository(this._processes, repositoryRoot, settings.CommitAuthor),
            new GitHubPullRequests(this._processes, github, settings.BaseBranch),
            settings.BaseBranch
        );
    }

    public Pipeline CreatePipeline(ConductorSettings settings, string repositoryRoot) =>
        new(this.CreateSpecifyCommand(settings, repositoryRoot), this.CreateBuildCommand(settings, repositoryRoot));

    public RunLoop CreateRunLoop(ConductorSettings settings, CommandContext context, RunLoopOptions options) =>
        new(
            new GitHubIssues(this._processes, GitHubOptionsFor(settings, context.RepositoryRoot)),
            this.CreatePipeline(settings, context.RepositoryRoot),
            options,
            context.Output,
            TimeProvider.System
        );

    // Building needs the checks code must pass; a project that names none builds nothing.
    private Option<BuildCommand> CreateBuildCommand(ConductorSettings settings, string repositoryRoot) =>
        settings.CodeGate is { Count: > 0 } codeGate
            ? this.BuildCommandFor(settings, repositoryRoot, codeGate)
            : Option<BuildCommand>.None;

    private BuildCommand BuildCommandFor(ConductorSettings settings, string repositoryRoot, List<string> codeGate)
    {
        GitHubOptions github = GitHubOptionsFor(settings, repositoryRoot);
        GitRepository git = new(this._processes, repositoryRoot, settings.CommitAuthor);
        BuildStage stage = new(
            new ClaudeCodeAgentRunner(this._processes),
            new CommandGate(this._processes, codeGate),
            git
        );
        return new BuildCommand(
            stage,
            new GitHubIssues(this._processes, github),
            git,
            new GitHubPullRequests(this._processes, github, settings.BaseBranch),
            settings.BaseBranch
        );
    }

    private static GitHubOptions GitHubOptionsFor(ConductorSettings settings, string repositoryRoot) =>
        new(settings.Repository, settings.Maintainers, repositoryRoot);
}