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

    private static ProcessOutcome Succeeded() => new(0, "", "", false);

    private static ProcessOutcome Failed() => new(1, "", "fatal", false);

    private void Responds(ProcessOutcome outcome) =>
        this._processes.Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>()).Returns(outcome);

    private List<(string Directory, string Arguments)> Commands() =>
        [
            .. this._processes
                .ReceivedCalls()
                .Select(call => (ProcessRequest)call.GetArguments()[0]!)
                .Select(request => (request.WorkingDirectory, string.Join(' ', request.Arguments))),
        ];
}