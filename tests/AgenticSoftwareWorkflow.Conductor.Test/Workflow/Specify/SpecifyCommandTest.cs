using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Specify;

public sealed class SpecifyCommandTest : IDisposable
{
    private const string Address = "https://github.com/owner/repository/pull/9";

    private static readonly WorkItemId Seven = new("github:owner/repository", "7");
    private static readonly WorkItem Item = new(Seven, "Add a clock", "Show the time.", ["ready"], []);
    private static readonly AgentUsage NoUsage = new(0, 0, 0m, 0, []);
    private static readonly GitIdentity Bot = new("repository-bot", "bot@example.com");

    private readonly IWorkSupplying _work = Substitute.For<IWorkSupplying>();
    private readonly IAgentic _agent = Substitute.For<IAgentic>();
    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();
    private readonly IChangeProposing _changes = Substitute.For<IChangeProposing>();
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aswf-command-");

    public SpecifyCommandTest()
    {
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Item));
        _ = this._work.Ask(Seven, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Fin.Succ(Unit.Default));
        _ = this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome(0, "", "", false));
        _ = this._changes.Propose(Arg.Any<ChangeProposal>(), Arg.Any<CancellationToken>()).Returns(Fin.Succ(Address));
    }

    [Fact]
    public async Task Run_WhenTheItemIsSpecified_ProposesTheSpecificationForReview()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        SpecifyCommand command = this.Command();
        Fin<string> report;

        // Act
        report = await command.Run(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            $"Proposed the specification for github:owner/repository#7 for review: {Address}",
            AssertSuccess(report)
        );
        _ = await this._changes.Received(1).Propose(
            Arg.Is<ChangeProposal>(proposal =>
                proposal.Branch == "aswf/specify-7"
                && proposal.Title == "Specification for github:owner/repository#7"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Run_WhenTheItemIsSpecified_CommitsPushesAndRemovesTheWorkspace()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        SpecifyCommand command = this.Command();

        // Act
        _ = await command.Run(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["fetch", "worktree", "add", "-c", "push", "worktree", "branch"], this.GitSubcommands());
    }

    [Fact]
    public async Task Run_WhenTheSpecifierAsks_LeavesTheItemWaitingAndProposesNothing()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"questions","questions":["Which zone?"]}""");
        SpecifyCommand command = this.Command();
        Fin<string> report;

        // Act
        report = await command.Run(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ("github:owner/repository#7 is waiting on answers to 1 question(s).", 0),
            (AssertSuccess(report), this._changes.ReceivedCalls().Count())
        );
    }

    [Fact]
    public async Task Run_WhenTheWorkspaceCannotBeCreated_FailsWithoutSpecifying()
    {
        // Arrange
        _ = this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome(128, "", "fatal", false));
        SpecifyCommand command = this.Command();
        Fin<string> report;

        // Act
        report = await command.Run(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<CommandFailed>(AssertFailure(report));
        Assert.Empty(this._agent.ReceivedCalls());
    }

    [Fact]
    public async Task Run_WhenSpecifyingFails_StillRemovesTheWorkspace()
    {
        // Arrange
        _ = this._agent
            .Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<AgentResult>(new AgentTimedOut(TimeSpan.FromMinutes(15))));
        SpecifyCommand command = this.Command();
        Fin<string> report;

        // Act
        report = await command.Run(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<AgentTimedOut>(AssertFailure(report));
        Assert.Equal(["fetch", "worktree", "worktree", "branch"], this.GitSubcommands());
    }

    [Fact]
    public async Task Run_WhenTheWorkspaceCannotBeRemoved_ReportsThatFailure()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"questions","questions":["Which zone?"]}""");
        _ = this._processes
            .Run(Arg.Is<ProcessRequest>(request => request.Arguments.Contains("remove")), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome(1, "", "locked", false));
        SpecifyCommand command = this.Command();
        Fin<string> report;

        // Act
        report = await command.Run(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("'git worktree remove' exited with code 1: locked", AssertFailure(report).Message);
    }

    public void Dispose() => this._root.Delete(recursive: true);

    private SpecifyCommand Command()
    {
        GitRepository git = new(this._processes, this._root.FullName, Bot);
        return new SpecifyCommand(this._work, this._agent, git, this._changes, "master");
    }

    private void SpecifierAnswers(string structuredOutput) =>
        this._agent
            .Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Succ(new AgentResult("", Prelude.Some(structuredOutput), NoUsage, [])));

    private List<string> GitSubcommands() =>
        [
            .. this._processes
                .ReceivedCalls()
                .Select(call => (ProcessRequest)call.GetArguments()[0]!)
                .Select(request => request.Arguments[0]),
        ];
}