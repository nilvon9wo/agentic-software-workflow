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
    public Task<Fin<Workspace>> CreateWorkspace(
        string branch,
        string baseBranch,
        CancellationToken cancellationToken
    ) =>
        this.AddWorkspace(branch, baseBranch, cancellationToken);

    /// <summary>
    /// A fresh working copy of a branch that already exists on the remote — a
    /// proposal under review — so a revision is added to it as a new commit.
    /// </summary>
    public Task<Fin<Workspace>> OpenWorkspace(string branch, CancellationToken cancellationToken) =>
        this.AddWorkspace(branch, branch, cancellationToken);

    /// <summary>
    /// Every path changed in the workspace since its last commit, new files
    /// included: what a worker that edits files itself has touched.
    /// </summary>
    public async Task<Fin<IReadOnlyList<string>>> ChangedPaths(Workspace workspace, CancellationToken cancellationToken)
    {
        CommandLineTool git = this.In(workspace);
        Fin<string> noted = await NoteNewFiles(git, cancellationToken);
        Fin<string> listed = await noted.Then(_ => git.Run(["diff", "--name-only"], cancellationToken));
        return listed.Map(Lines);
    }

    /// <summary>
    /// The workspace's changes since its last commit, new files included, as a
    /// diff: how a reviewer that may only read sees what was done.
    /// </summary>
    public async Task<Fin<string>> Diff(Workspace workspace, CancellationToken cancellationToken)
    {
        CommandLineTool git = this.In(workspace);
        Fin<string> noted = await NoteNewFiles(git, cancellationToken);
        return await noted.Then(_ => git.Run(["diff", "--no-color"], cancellationToken));
    }

    /// <summary>The files in a directory of a remote branch as it is now, after fetching it.</summary>
    public async Task<Fin<IReadOnlyList<string>>> FilesOnRemote(
        string branch,
        string directory,
        CancellationToken cancellationToken
    )
    {
        Fin<string> fetched = await this._git.Run(["fetch", Remote, branch], cancellationToken);
        Fin<string> listed = await fetched.Then(
            _ => this._git.Run(["ls-tree", "--name-only", $"{Remote}/{branch}", $"{directory}/"], cancellationToken)
        );
        return listed.Map(Lines);
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

    private async Task<Fin<Workspace>> AddWorkspace(
        string branch,
        string startingFrom,
        CancellationToken cancellationToken
    )
    {
        string path = this.WorkspacePathFor(branch);
        Fin<string> fetched = await this._git.Run(["fetch", Remote, startingFrom], cancellationToken);
        Fin<string> added = await fetched.Then(
            _ => this._git.Run(
                ["worktree", "add", "-b", branch, path, $"{Remote}/{startingFrom}"],
                cancellationToken
            )
        );
        return added.Map(_ => new Workspace(path, branch));
    }

    private static IReadOnlyList<string> Lines(string output) =>
        [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    // Untracked files are invisible to `git diff`; noting them as intended for
    // the next commit makes them appear, without staging their content.
    private static Task<Fin<string>> NoteNewFiles(CommandLineTool git, CancellationToken cancellationToken) =>
        git.Run(["add", "--intent-to-add", "--all"], cancellationToken);

    private string WorkspacePathFor(string branch)
    {
        string directoryName = branch.Replace(BranchSeparator, PathSafeSeparator);
        return Path.Combine([this._repositoryRoot, .. WorkspacesDirectory, directoryName]);
    }

    private CommandLineTool In(Workspace workspace) => new(this._processes, Git, workspace.Path);
}