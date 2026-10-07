using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Gates;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Build;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Build;

public sealed class BuildCommandTest : IDisposable
{
    private const string Address = "https://github.com/owner/repository/pull/12";
    private const string SpecificationFile = "spec/7-add-a-clock.md";

    private static readonly WorkItemId Seven = new("github:owner/repository", "7");
    private static readonly WorkItemId Eight = new("github:owner/repository", "8");
    private static readonly WorkItem Item = new(Seven, "Add a clock", "Show the time.", ["specified"], [], false);
    private static readonly AgentUsage NoUsage = new(0, 0, 0m, 0, []);

    private readonly IWorkSupplying _work = Substitute.For<IWorkSupplying>();
    private readonly IAgentic _agent = Substitute.For<IAgentic>();
    private readonly IGateKeeping _gate = Substitute.For<IGateKeeping>();
    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();
    private readonly IChangeProposing _changes = Substitute.For<IChangeProposing>();
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aswf-build-");

    public BuildCommandTest()
    {
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Item));
        _ = this._work
            .Read(Eight, Arg.Any<CancellationToken>())
            .Returns(Fin.Succ(Item with { Id = Eight, Title = "Not merged yet" }));
        _ = this._work.MarkBuilt(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Unit.Default));
        _ = this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => GitAnswer((ProcessRequest)call[0]));
        _ = this._agent
            .Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(call => AgentAnswer((AgentTask)call[0]));
        _ = this._gate.Check(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Fin.Succ(Unit.Default));
        _ = this._changes.Propose(Arg.Any<ChangeProposal>(), Arg.Any<CancellationToken>()).Returns(Fin.Succ(Address));
        string specificationPath = Path.Combine(this.WorkspacePath, "spec", "7-add-a-clock.md");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(specificationPath)!);
        File.WriteAllText(specificationPath, "# Show the time in UTC\n\n- **AC-1** …\n");
    }

    private string WorkspacePath => Path.Combine(this._root.FullName, ".aswf", "worktrees", "aswf-build-7");

    [Fact]
    public async Task ListBuildable_WhenOnlySomeSpecificationsHaveMerged_ListsThoseItems()
    {
        // Arrange
        _ = this._work
            .ListSpecified(Arg.Any<CancellationToken>())
            .Returns(Fin.Succ<IReadOnlyList<WorkItemId>>([Seven, Eight]));
        BuildCommand command = this.Command();

        // Act
        Fin<IReadOnlyList<WorkItemId>> buildable = await command.ListBuildable(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([Seven], AssertSuccess(buildable));
    }

    [Fact]
    public async Task ListBuildable_WhenAnItemCannotBeRead_LeavesItOut()
    {
        // Arrange
        _ = this._work
            .ListSpecified(Arg.Any<CancellationToken>())
            .Returns(Fin.Succ<IReadOnlyList<WorkItemId>>([Seven]));
        _ = this._work
            .Read(Seven, Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<WorkItem>(new WorkResponseMalformed("unreadable")));
        BuildCommand command = this.Command();

        // Act
        Fin<IReadOnlyList<WorkItemId>> buildable = await command.ListBuildable(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(AssertSuccess(buildable));
    }

    [Fact]
    public async Task Build_WhenTheBuildIsAccepted_ProposesItForAMaintainerToMerge()
    {
        // Arrange
        BuildCommand command = this.Command();

        // Act
        Fin<string> report = await command.Build(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal($"Proposed the build of github:owner/repository#7 for review: {Address}", AssertSuccess(report));
        _ = await this._changes.Received(1).Propose(
            Arg.Is<ChangeProposal>(
                proposal => proposal.Kind == ProposalKind.Implementation
                    && proposal.Branch == "aswf/build-7"
                    && proposal.Title == "Build: Show the time in UTC (#7)"
                    && proposal.Description.Contains("Tests for AC-1.")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Build_WhenTheBuildIsAccepted_CommitsExactlyWhatTheWorkersChanged()
    {
        // Arrange
        BuildCommand command = this.Command();

        // Act
        _ = await command.Build(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("add -- src/Clock.cs tests/ClockTest.cs", this.GitCommands());
    }

    [Fact]
    public async Task Build_WhenTheBuildIsProposed_MarksTheItemBuilt()
    {
        // Arrange
        BuildCommand command = this.Command();

        // Act
        _ = await command.Build(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = await this._work.Received(1).MarkBuilt(Seven, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Build_WhenTheSpecificationIsMissing_FailsAndStillRemovesTheWorkspace()
    {
        // Arrange
        File.Delete(Path.Combine(this.WorkspacePath, "spec", "7-add-a-clock.md"));
        BuildCommand command = this.Command();

        // Act
        Fin<string> report = await command.Build(Seven, TestContext.Current.CancellationToken);

        // Assert
        SpecificationNotFound missing = Assert.IsType<SpecificationNotFound>(AssertFailure(report));
        Assert.Equal(
            ((Seven, SpecificationFile), true),
            (
                (missing.Item, missing.Path),
                this.GitCommands().Exists(git => git.StartsWith("worktree remove", StringComparison.Ordinal))
            )
        );
    }

    public void Dispose() => this._root.Delete(recursive: true);

    private static ProcessOutcome GitAnswer(ProcessRequest request)
    {
        string arguments = string.Join(' ', request.Arguments);
        string output = arguments switch
        {
            "diff --name-only" => "src/Clock.cs\ntests/ClockTest.cs\n",
            _ when arguments.StartsWith("ls-tree", StringComparison.Ordinal) => $"{SpecificationFile}\n",
            _ => "",
        };
        return new ProcessOutcome(0, output, "", false);
    }

    private static Fin<AgentResult> AgentAnswer(AgentTask task)
    {
        string answer = task.OutputSchema.Match(
            Some: schema => schema.Contains("verdict", StringComparison.Ordinal)
                ? """{"verdict":"approve","findings":[]}"""
                : """{"summary":"Tests for AC-1."}""",
            None: () => "{}"
        );
        return Fin.Succ(new AgentResult("", Prelude.Some(answer), NoUsage, []));
    }

    private BuildCommand Command()
    {
        GitRepository git = new(this._processes, this._root.FullName, new GitIdentity("bot", "bot@example.com"));
        BuildStage stage = new(this._agent, this._gate, git);
        return new BuildCommand(stage, this._work, git, this._changes, "master");
    }

    private List<string> GitCommands() =>
        [
            .. this._processes
                .ReceivedCalls()
                .Select(call => (ProcessRequest)call.GetArguments()[0]!)
                .Select(request => string.Join(' ', request.Arguments)),
        ];
}