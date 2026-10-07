using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Gates;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;
using LanguageExt.Common;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Specify;

public sealed class SpecifyStageTest : IDisposable
{
    private const string Specification = "## Summary\n\nShow the time in UTC.";

    private static readonly WorkItemId Seven = new("github:owner/repository", "7");
    private static readonly WorkItem Item = new(Seven, "Add a clock", "Show the time.", ["ready"], [], false);
    private static readonly AgentUsage NoUsage = new(0, 0, 0m, 0, []);

    private static readonly ProposalReview UnderReview = new(
        Seven,
        "https://github.com/owner/repository/pull/9",
        "aswf/specify-7",
        [new WorkComment("maintainer", "Make it general.", true, false)]
    );

    private static readonly WorkItem Answered = Item with
    {
        Comments =
        [
            new WorkComment("repository-bot", "Which zone?", false, true),
            new WorkComment("maintainer", "Use UTC.", true, false),
        ],
        IsWaiting = true,
    };

    private readonly IWorkSupplying _work = Substitute.For<IWorkSupplying>();
    private readonly IAgentic _agent = Substitute.For<IAgentic>();
    private readonly IGateKeeping _gate = Substitute.For<IGateKeeping>();
    private readonly DirectoryInfo _workspace = Directory.CreateTempSubdirectory("aswf-specify-");

    public SpecifyStageTest()
    {
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Item));
        _ = this._work.Ask(Seven, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Fin.Succ(Unit.Default));
        this.GateAnswers(Fin.Succ(Unit.Default));
    }

    [Fact]
    public async Task Run_WhenTheSpecifierSpecifies_WritesTheSpecificationFile()
    {
        // Arrange
        this.SpecifierAnswers($$"""{"outcome":"specified","specification":"{{Specification.Replace("\n", "\\n")}}"}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new Specified("spec/7-add-a-clock.md"), AssertSuccess(outcome));
        string written = await File.ReadAllTextAsync(
            Path.Combine(this._workspace.FullName, "spec", "7-add-a-clock.md"),
            TestContext.Current.CancellationToken
        );
        Assert.Equal(Specification, written);
    }

    [Fact]
    public async Task Run_WhenTheSpecifierAsks_PostsTheQuestionsAndAwaitsAnswers()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"questions","questions":["Which zone?","Which format?"]}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        AwaitingAnswers awaiting = Assert.IsType<AwaitingAnswers>(AssertSuccess(outcome));
        Assert.Equal(["Which zone?", "Which format?"], awaiting.Questions);
        _ = await this._work.Received(1).Ask(
            Seven,
            "**Questions before this can be specified**\n\n1. Which zone?\n2. Which format?\n\n"
            + "Please reply in this thread. Only maintainers' replies are read.\n",
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Run_WhenBriefingTheSpecifier_GivesItsRoleInstructionsAndContract()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"questions","questions":["Which zone?"]}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        _ = await stage.Run(Seven, this._workspace.FullName, TestContext.Current.CancellationToken);

        // Assert
        AgentTask task = (AgentTask)this._agent.ReceivedCalls().Single().GetArguments()[0]!;
        Assert.Equal(
            (
                WorkflowRoles.Specifier,
                WorkItemBrief.Describe(Item),
                this._workspace.FullName,
                Prelude.Some(RoleInstructions.Specifier),
                Prelude.Some(SpecifierAnswer.Schema)
            ),
            (task.Role, task.Prompt, task.WorkingDirectory, task.Instructions, task.OutputSchema)
        );
    }

    [Fact]
    public async Task Run_WhenTheItemCannotBeRead_FailsWithoutRunningTheSpecifier()
    {
        // Arrange
        Error unreadable = new WorkResponseMalformed("unreadable");
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Fail<WorkItem>(unreadable));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((unreadable, 0), (AssertFailure(outcome), this._agent.ReceivedCalls().Count()));
    }

    [Fact]
    public async Task Run_WhenTheSpecifierRunFails_FailsWithItsError()
    {
        // Arrange
        _ = this._agent
            .Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<AgentResult>(new AgentTimedOut(TimeSpan.FromMinutes(15))));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        _ = Assert.IsType<AgentTimedOut>(AssertFailure(outcome));
    }

    [Fact]
    public async Task Run_WhenThereIsNoStructuredOutput_FailsWithUnusableAnswer()
    {
        // Arrange
        _ = this._agent
            .Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Succ(new AgentResult("prose only", Option<string>.None, NoUsage, [])));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            "The specifier's answer cannot be used: there was no structured output",
            AssertFailure(outcome).Message
        );
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"outcome":"specified","specification":""}""")]
    [InlineData("""{"outcome":"questions","questions":[]}""")]
    [InlineData("""{"outcome":"shrug"}""")]
    public async Task Run_WhenTheAnswerCannotBeActedOn_FailsWithUnusableAnswer(string answer)
    {
        // Arrange
        this.SpecifierAnswers(answer);
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        _ = Assert.IsType<SpecifierAnswerUnusable>(AssertFailure(outcome));
    }

    [Fact]
    public async Task Run_WhenPostingTheQuestionsFails_FailsWithThatError()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"questions","questions":["Which zone?"]}""");
        _ = this._work
            .Ask(Seven, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<Unit>(new CommandFailed("gh issue comment", 1, "rate limited")));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        _ = Assert.IsType<CommandFailed>(AssertFailure(outcome));
    }

    [Fact]
    public async Task Run_WhenTheKeyIsNotAFileName_WritesAFileNamedSafely()
    {
        // Arrange
        WorkItemId nested = new("jira:PROJECT", "PROJECT/12");
        _ = this._work.Read(nested, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Item with { Id = nested }));
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            nested,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new Specified("spec/PROJECT-12-add-a-clock.md"), AssertSuccess(outcome));
    }

    [Fact]
    public async Task Run_WhenTheItemIsStillWaiting_ReportsItWithoutRunningTheSpecifier()
    {
        // Arrange
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Item with { IsWaiting = true }));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((new StillWaiting(), 0), (AssertSuccess(outcome), this._agent.ReceivedCalls().Count()));
    }

    [Fact]
    public async Task Run_WhenTheItemHasBeenAnswered_ResolvesItBeforeSpecifying()
    {
        // Arrange
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Answered));
        _ = this._work.Resolve(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Unit.Default));
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        _ = AssertSuccess(outcome);
        Received.InOrder(
            () =>
            {
                _ = this._work.Resolve(Seven, Arg.Any<CancellationToken>());
                _ = this._agent.Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>());
            }
        );
    }

    [Fact]
    public async Task Run_WhenResolvingTheAnsweredItemFails_FailsWithoutRunningTheSpecifier()
    {
        // Arrange
        Error rateLimited = new CommandFailed("gh issue edit", 1, "rate limited");
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Answered));
        _ = this._work.Resolve(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Fail<Unit>(rateLimited));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((rateLimited, 0), (AssertFailure(outcome), this._agent.ReceivedCalls().Count()));
    }

    [Fact]
    public async Task Run_WhenTheSpecificationFailsTheChecks_GivesTheSpecifierTheReportToRepairIt()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        this.GateAnswers(Fin.Fail<Unit>(new GatesFailed("spec/7.md:3 MD013 line too long")), Fin.Succ(Unit.Default));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        _ = await stage.Run(Seven, this._workspace.FullName, TestContext.Current.CancellationToken);

        // Assert
        _ = await this._agent.Received(1).Run(
            Arg.Is<AgentTask>(
                task => task.Prompt.Contains("spec/7.md:3 MD013 line too long")
                    && task.Prompt.Contains("Spec.")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Run_WhenTheRepairPassesTheChecks_ReportsTheSpecification()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        this.GateAnswers(Fin.Fail<Unit>(new GatesFailed("MD013")), Fin.Succ(Unit.Default));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new Specified("spec/7-add-a-clock.md"), AssertSuccess(outcome));
    }

    [Fact]
    public async Task Run_WhenTheRepairStillFailsTheChecks_FailsWithTheirReportAfterOneRepair()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        GatesFailed stillFailing = new("MD013");
        this.GateAnswers(Fin.Fail<Unit>(new GatesFailed("MD029")), Fin.Fail<Unit>(stillFailing));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Run(
            Seven,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((stillFailing, 2), (AssertFailure(outcome), this._agent.ReceivedCalls().Count()));
    }

    [Fact]
    public async Task Run_WhenTheSpecifierAsks_RunsNoChecks()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"questions","questions":["Which zone?"]}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        _ = await stage.Run(Seven, this._workspace.FullName, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(this._gate.ReceivedCalls());
    }

    [Fact]
    public async Task Revise_WhenGivenAReview_BriefsTheSpecifierWithItsSpecificationAndTheFeedback()
    {
        // Arrange
        string specDirectory = Path.Combine(this._workspace.FullName, "spec");
        _ = Directory.CreateDirectory(specDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(specDirectory, "7-add-a-clock.md"),
            "# Show the time\n\nThe first draft.\n",
            TestContext.Current.CancellationToken
        );
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        _ = await stage.Revise(UnderReview, this._workspace.FullName, TestContext.Current.CancellationToken);

        // Assert
        _ = await this._agent.Received(1).Run(
            Arg.Is<AgentTask>(
                task => task.Prompt.Contains("The first draft.")
                    && task.Prompt.Contains("### maintainer reviewed\n\nMake it general.")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Revise_WhenTheSpecificationFileIsMissing_SaysSoInTheBrief()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Spec."}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        _ = await stage.Revise(UnderReview, this._workspace.FullName, TestContext.Current.CancellationToken);

        // Assert
        _ = await this._agent.Received(1).Run(
            Arg.Is<AgentTask>(task => task.Prompt.Contains("The specification file was not found")),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Revise_WhenTheRevisionPassesTheChecks_RewritesTheSpecification()
    {
        // Arrange
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Revised."}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Revise(
            UnderReview,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(new Specified("spec/7-add-a-clock.md"), AssertSuccess(outcome));
    }

    [Fact]
    public async Task Revise_WhenTheItemIsStillWaitingOnAnAnswer_WaitsWithoutRunningTheSpecifier()
    {
        // Arrange
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Item with { IsWaiting = true }));
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        Fin<SpecifyOutcome> outcome = await stage.Revise(
            UnderReview,
            this._workspace.FullName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((new StillWaiting(), 0), (AssertSuccess(outcome), this._agent.ReceivedCalls().Count()));
    }

    [Fact]
    public async Task Revise_WhenTheItemsQuestionHasBeenAnswered_ResolvesItBeforeRevising()
    {
        // Arrange
        _ = this._work.Read(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Answered));
        _ = this._work.Resolve(Seven, Arg.Any<CancellationToken>()).Returns(Fin.Succ(Unit.Default));
        this.SpecifierAnswers("""{"outcome":"specified","specification":"Revised."}""");
        SpecifyStage stage = new(this._work, this._agent, this._gate);

        // Act
        _ = await stage.Revise(UnderReview, this._workspace.FullName, TestContext.Current.CancellationToken);

        // Assert
        Received.InOrder(
            () =>
            {
                _ = this._work.Resolve(Seven, Arg.Any<CancellationToken>());
                _ = this._agent.Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>());
            }
        );
    }

    public void Dispose() => this._workspace.Delete(recursive: true);

    private void GateAnswers(Fin<Unit> first, params Fin<Unit>[] rest) =>
        this._gate
            .Check(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(first, rest);

    private void SpecifierAnswers(string structuredOutput) =>
        this._agent
            .Run(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Succ(new AgentResult("", Prelude.Some(structuredOutput), NoUsage, [])));
}