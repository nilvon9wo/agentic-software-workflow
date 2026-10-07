using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Git;

public sealed class GitRepositoryTest
{
    private const string Root = "/repository";

    private static readonly GitIdentity Bot = new("repository-bot", "bot@example.com");
    private static readonly string WorkspacePath = Path.Combine(Root, ".aswf", "worktrees", "aswf-specify-7");
    private static readonly Workspace Workspace = new(WorkspacePath, "aswf/specify-7");

    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();

    public GitRepositoryTest() => this.Responds(Succeeded());

    [Fact]
    public async Task CreateWorkspace_WhenCalled_BranchesAFreshWorktreeFromTheRemoteBase()
    {
        // Arrange
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<Workspace> created = await repository.CreateWorkspace(
            "aswf/specify-7",
            "master",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(Workspace, AssertSuccess(created));
        Assert.Equal(
            [
                (Root, "fetch origin master"),
                (Root, $"worktree add -b aswf/specify-7 {WorkspacePath} origin/master"),
            ],
            this.Commands()
        );
    }

    [Fact]
    public async Task OpenWorkspace_WhenCalled_ChecksOutTheExistingRemoteBranch()
    {
        // Arrange
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<Workspace> opened = await repository.OpenWorkspace("aswf/specify-7", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Workspace, AssertSuccess(opened));
        Assert.Equal(
            [
                (Root, "fetch origin aswf/specify-7"),
                (Root, $"worktree add -b aswf/specify-7 {WorkspacePath} origin/aswf/specify-7"),
            ],
            this.Commands()
        );
    }

    [Fact]
    public async Task CreateWorkspace_WhenFetchingFails_CreatesNoWorktree()
    {
        // Arrange
        this.Responds(Failed());
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<Workspace> created = await repository.CreateWorkspace(
            "aswf/specify-7",
            "master",
            TestContext.Current.CancellationToken
        );

        // Assert
        _ = Assert.IsType<CommandFailed>(AssertFailure(created));
        _ = Assert.Single(this.Commands());
    }

    [Fact]
    public async Task Commit_WhenCalled_CommitsExactlyThosePathsAsTheWorkersIdentity()
    {
        // Arrange
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<Unit> committed = await repository.Commit(
            Workspace,
            ["spec/7.md"],
            "Specify 7",
            TestContext.Current.CancellationToken
        );

        // Assert
        _ = AssertSuccess(committed);
        Assert.Equal(
            [
                (WorkspacePath, "add -- spec/7.md"),
                (
                    WorkspacePath,
                    "-c user.name=repository-bot -c user.email=bot@example.com commit --message Specify 7"
                ),
            ],
            this.Commands()
        );
    }

    [Fact]
    public async Task Push_WhenCalled_PushesTheWorkspaceBranchToTheRemote()
    {
        // Arrange
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<Unit> pushed = await repository.Push(Workspace, TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(pushed);
        Assert.Equal([(WorkspacePath, "push --set-upstream origin aswf/specify-7")], this.Commands());
    }

    [Fact]
    public async Task RemoveWorkspace_WhenCalled_RemovesTheWorktreeThenItsLocalBranch()
    {
        // Arrange
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<Unit> removed = await repository.RemoveWorkspace(Workspace, TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(removed);
        Assert.Equal(
            [
                (Root, $"worktree remove --force {WorkspacePath}"),
                (Root, "branch --delete --force aswf/specify-7"),
            ],
            this.Commands()
        );
    }

    [Fact]
    public async Task ChangedPaths_WhenCalled_ListsEveryChangedFileIncludingNewOnes()
    {
        // Arrange
        this.Responds(Succeeded(), Succeeded(" src/Clock.cs \ntests/ClockTest.cs\n\n"));
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<IReadOnlyList<string>> changed = await repository.ChangedPaths(
            Workspace,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(["src/Clock.cs", "tests/ClockTest.cs"], AssertSuccess(changed));
    }

    [Fact]
    public async Task ChangedPaths_WhenCalled_NotesNewFilesBeforeListingTheChanges()
    {
        // Arrange
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        _ = await repository.ChangedPaths(Workspace, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [(WorkspacePath, "add --intent-to-add --all"), (WorkspacePath, "diff --name-only")],
            this.Commands()
        );
    }

    [Fact]
    public async Task Diff_WhenCalled_ReturnsTheWorkspacesChangesIncludingNewFiles()
    {
        // Arrange
        this.Responds(Succeeded(), Succeeded("diff --git a/src/Clock.cs b/src/Clock.cs"));
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<string> diff = await repository.Diff(Workspace, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ("diff --git a/src/Clock.cs b/src/Clock.cs", "diff --no-color"),
            (AssertSuccess(diff), this.Commands()[1].Arguments)
        );
    }

    [Fact]
    public async Task FilesOnRemote_WhenCalled_ListsTheDirectoryOnTheFreshlyFetchedBranch()
    {
        // Arrange
        this.Responds(Succeeded(), Succeeded("spec/7-add-a-clock.md\nspec/8-other.md\n"));
        GitRepository repository = new(this._processes, Root, Bot);

        // Act
        Fin<IReadOnlyList<string>> files = await repository.FilesOnRemote(
            "master",
            "spec",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            ("spec/7-add-a-clock.md|spec/8-other.md", "ls-tree --name-only origin/master spec/"),
            (string.Join('|', AssertSuccess(files)), this.Commands()[1].Arguments)
        );
    }

    private static ProcessOutcome Succeeded() => Succeeded("");

    private static ProcessOutcome Succeeded(string output) => new(0, output, "", false);

    private static ProcessOutcome Failed() => new(1, "", "fatal", false);

    private void Responds(ProcessOutcome first, params ProcessOutcome[] rest) =>
        this._processes.Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>()).Returns(first, rest);

    private List<(string Directory, string Arguments)> Commands() =>
        [
            .. this._processes
                .ReceivedCalls()
                .Select(call => (ProcessRequest)call.GetArguments()[0]!)
                .Select(request => (request.WorkingDirectory, string.Join(' ', request.Arguments))),
        ];
}