using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Git;

/// <summary>
/// The repository the workflow works on, through git: isolated workspaces
/// (git worktrees) branched from the latest base branch, and committing and
/// pushing what is done in them.
/// </summary>
public sealed class GitRepository(IProcessCapable processes, string repositoryRoot, GitIdentity identity)
{
    private const string Git = "git";
    private const string Remote = "origin";
    private const char BranchSeparator = '/';
    private const char PathSafeSeparator = '-';

    private static readonly string[] WorkspacesDirectory = [".aswf", "worktrees"];

    private readonly IProcessCapable _processes = processes;
    private readonly string _repositoryRoot = repositoryRoot;
    private readonly GitIdentity _identity = identity;
    private readonly CommandLineTool _git = new(processes, Git, repositoryRoot);

    /// <summary>
    /// A new workspace on a new branch, starting from the remote's latest
    /// <paramref name="baseBranch"/> — never from whatever happens to be checked out.
    /// </summary>
    public async Task<Fin<Workspace>> CreateWorkspace(
        string branch,
        string baseBranch,
        CancellationToken cancellationToken
    )
    {
        string path = this.WorkspacePathFor(branch);
        Fin<string> fetched = await this._git.Run(["fetch", Remote, baseBranch], cancellationToken);
        Fin<string> added = await fetched.Then(
            _ => this._git.Run(
                ["worktree", "add", "-b", branch, path, $"{Remote}/{baseBranch}"],
                cancellationToken
            )
        );
        return added.Map(_ => new Workspace(path, branch));
    }

    /// <summary>Commits exactly the named paths, as the workers' identity.</summary>
    public async Task<Fin<Unit>> Commit(
        Workspace workspace,
        IReadOnlyList<string> paths,
        string message,
        CancellationToken cancellationToken
    )
    {
        CommandLineTool git = this.In(workspace);
        Fin<string> staged = await git.Run(["add", "--", .. paths], cancellationToken);
        Fin<string> committed = await staged.Then(
            _ => git.Run(
                [
                    "-c",
                    $"user.name={this._identity.Name}",
                    "-c",
                    $"user.email={this._identity.Email}",
                    "commit",
                    "--message",
                    message,
                ],
                cancellationToken
            )
        );
        return committed.Map(_ => Unit.Default);
    }

    public async Task<Fin<Unit>> Push(Workspace workspace, CancellationToken cancellationToken)
    {
        Fin<string> pushed = await this.In(workspace).Run(
            ["push", "--set-upstream", Remote, workspace.Branch],
            cancellationToken
        );
        return pushed.Map(_ => Unit.Default);
    }

    /// <summary>Removes the workspace and its local branch; anything pushed stays on the remote.</summary>
    public async Task<Fin<Unit>> RemoveWorkspace(Workspace workspace, CancellationToken cancellationToken)
    {
        Fin<string> removed = await this._git.Run(
            ["worktree", "remove", "--force", workspace.Path],
            cancellationToken
        );
        Fin<string> deleted = await removed.Then(
            _ => this._git.Run(["branch", "--delete", "--force", workspace.Branch], cancellationToken)
        );
        return deleted.Map(_ => Unit.Default);
    }

    private string WorkspacePathFor(string branch)
    {
        string directoryName = branch.Replace(BranchSeparator, PathSafeSeparator);
        return Path.Combine([this._repositoryRoot, .. WorkspacesDirectory, directoryName]);
    }

    private CommandLineTool In(Workspace workspace) => new(this._processes, Git, workspace.Path);
}