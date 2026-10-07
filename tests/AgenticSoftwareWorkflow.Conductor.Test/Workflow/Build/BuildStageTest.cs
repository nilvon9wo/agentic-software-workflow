using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Gates;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Build;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Build;

public sealed class BuildStageTest
{
    private const string Approve = """{"verdict":"approve","findings":[]}""";
    private const string Revise = """{"verdict":"revise","findings":["AC-2 has no test."]}""";

    private static readonly WorkItemId Seven = new("github:owner/repository", "7");
    private static readonly WorkItem Item = new(Seven, "Add a clock", "Show the time.", ["specified"], [], false);
    private static readonly Workspace Workspace = new("/repository/.aswf/worktrees/aswf-build-7", "aswf/build-7");
    private static readonly BuildJob Job = new(Item, "# Show the time\n\n- **AC-1** …", Workspace);
    private static readonly AgentUsage NoUsage = new(0, 0, 0m, 0, []);

    private readonly IAgentic _agent = Substitute.For<IAgentic>();
    private readonly IGateKeeping _gate = Substitute.For<IGateKeeping>();
    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();

    public BuildStageTest()
    {
        _ = this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome(0, "diff --git a/src/Clock.cs b/src/Clock.cs", "", false));
        this.Answers(WorkflowRoles.TestAuthor, Report("Tests for AC-1."));
        this.Answers(WorkflowRoles.TestReviewer, Approve);
        this.Answers(WorkflowRoles.Implementer, Report("Added Clock."));
        this.Answers(WorkflowRoles.CodeReviewer, Approve);
        this.GateAnswers(Fin.Succ(Unit.Default));
    }

    [Fact]
    public async Task Run_WhenEveryStepIsAccepted_ReportsWhatTheWorkersDid()
    {
        // Arrange
        BuildStage stage = this.Stage();

        // Act
        Fin<Built> built = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        Built done = AssertSuccess(built);
        Assert.Equal(("Tests for AC-1.", "Added Clock."), (done.TestsSummary, done.ImplementationSummary));
    }

    [Fact]
    public async Task Run_WhenTheTestReviewerAsksForMore_GivesTheTestAuthorItsFindings()
    {
        // Arrange
        this.Answers(WorkflowRoles.TestReviewer, Revise, Approve);
        BuildStage stage = this.Stage();

        // Act
        _ = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        _ = await this._agent.Received(1).Run(
            Arg.Is<AgentTask>(
                task => task.Role == WorkflowRoles.TestAuthor && task.Prompt.Contains("- AC-2 has no test.")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Run_WhenTheTestsAreNeverAccepted_GivesUpAfterThreeAttemptsWithTheFindings()
    {
        // Arrange
        this.Answers(WorkflowRoles.TestReviewer, Revise);
        BuildStage stage = this.Stage();

        // Act
        Fin<Built> built = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        BuildRejected rejected = Assert.IsType<BuildRejected>(AssertFailure(built));
        Assert.Equal(
            ("tests", "- AC-2 has no test.", 3),
            (rejected.Stage, rejected.Findings, this.CallsTo(WorkflowRoles.TestAuthor))
        );
    }

    [Fact]
    public async Task Run_WhenTheChecksFail_GivesTheImplementerTheirReport()
    {
        // Arrange
        this.GateAnswers(Fin.Fail<Unit>(new GatesFailed("Clock.cs:3 CA1822")), Fin.Succ(Unit.Default));
        BuildStage stage = this.Stage();

        // Act
        _ = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        _ = await this._agent.Received(1).Run(
            Arg.Is<AgentTask>(
                task => task.Role == WorkflowRoles.Implementer && task.Prompt.Contains("Clock.cs:3 CA1822")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Run_WhenTheCodeReviewerAsksForChanges_GivesTheImplementerItsFindings()
    {
        // Arrange
        this.Answers(WorkflowRoles.CodeReviewer, Revise, Approve);
        BuildStage stage = this.Stage();

        // Act
        Fin<Built> built = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((true, 2), (built.IsSucc, this.CallsTo(WorkflowRoles.Implementer)));
    }

    [Fact]
    public async Task Run_WhenTheImplementationIsNeverAccepted_GivesUpAfterThreeAttempts()
    {
        // Arrange
        this.Answers(WorkflowRoles.CodeReviewer, Revise);
        BuildStage stage = this.Stage();

        // Act
        Fin<Built> built = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        BuildRejected rejected = Assert.IsType<BuildRejected>(AssertFailure(built));
        Assert.Equal(("implementation", 3), (rejected.Stage, this.CallsTo(WorkflowRoles.Implementer)));
    }

    [Fact]
    public async Task Run_WhenTheImplementerItselfFails_StopsWithoutRetrying()
    {
        // Arrange
        AgentTimedOut timedOut = new(TimeSpan.FromMinutes(30));
        _ = this._agent
            .Run(Arg.Is<AgentTask>(task => task.Role == WorkflowRoles.Implementer), Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<AgentResult>(timedOut));
        BuildStage stage = this.Stage();

        // Act
        Fin<Built> built = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((timedOut, 1), (AssertFailure(built), this.CallsTo(WorkflowRoles.Implementer)));
    }

    [Fact]
    public async Task Run_WhenReviewing_ShowsTheReviewerTheChangeAsADiff()
    {
        // Arrange
        BuildStage stage = this.Stage();

        // Act
        _ = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        _ = await this._agent.Received(1).Run(
            Arg.Is<AgentTask>(
                task => task.Role == WorkflowRoles.CodeReviewer
                    && task.Prompt.Contains("````diff\ndiff --git a/src/Clock.cs b/src/Clock.cs\n````")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Run_WhenAWorkersReportHasNoSummary_ReportsItAsEmpty()
    {
        // Arrange
        this.Answers(WorkflowRoles.TestAuthor, """{"summary":null}""");
        BuildStage stage = this.Stage();

        // Act
        Fin<Built> built = await stage.Run(Job, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("", AssertSuccess(built).TestsSummary);
    }

    private static string Report(string summary) => $$"""{"summary":"{{summary}}"}""";

    private BuildStage Stage() =>
        new(this._agent, this._gate, new GitRepository(this._processes, "/repository", new GitIdentity("bot", "b@x")));

    private void Answers(AgentRole role, string first, params string[] rest) =>
        this._agent
            .Run(Arg.Is<AgentTask>(task => task.Role == role), Arg.Any<CancellationToken>())
            .Returns(Answer(first), [.. rest.Select(Answer)]);

    private static Fin<AgentResult> Answer(string structuredOutput) =>
        Fin.Succ(new AgentResult("", Prelude.Some(structuredOutput), NoUsage, []));

    private void GateAnswers(Fin<Unit> first, params Fin<Unit>[] rest) =>
        this._gate.Check(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(first, rest);

    private int CallsTo(AgentRole role) =>
        this._agent
            .ReceivedCalls()
            .Count(call => ((AgentTask)call.GetArguments()[0]!).Role == role);
}