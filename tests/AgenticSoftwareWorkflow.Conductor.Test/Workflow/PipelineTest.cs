using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Gates;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Build;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow;

public sealed class PipelineTest
{
    private static readonly WorkItemId Seven = new("github:owner/repository", "7");

    private readonly IWorkSupplying _work = Substitute.For<IWorkSupplying>();
    private readonly IAgentic _agent = Substitute.For<IAgentic>();
    private readonly IGateKeeping _gate = Substitute.For<IGateKeeping>();
    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();
    private readonly IChangeProposing _changes = Substitute.For<IChangeProposing>();

    public PipelineTest()
    {
        _ = this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessOutcome(0, "", "", false));
        _ = this._work.ListSpecified(Arg.Any<CancellationToken>()).Returns(Fin.Succ<IReadOnlyList<WorkItemId>>([]));
        _ = this._changes
            .ListOpenSpecifications(Arg.Any<CancellationToken>())
            .Returns(Fin.Succ<IReadOnlyList<WorkItemId>>([]));
    }

    [Fact]
    public async Task ListBuildable_WhenTheProjectNamesNoCodeGate_ListsNothing()
    {
        // Arrange
        Pipeline pipeline = new(this.Specify(), Option<BuildCommand>.None);

        // Act
        Fin<IReadOnlyList<WorkItemId>> buildable = await pipeline.ListBuildable(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(AssertSuccess(buildable));
    }

    [Fact]
    public async Task Build_WhenTheProjectNamesNoCodeGate_RefusesToBuild()
    {
        // Arrange
        Pipeline pipeline = new(this.Specify(), Option<BuildCommand>.None);

        // Act
        Fin<string> report = await pipeline.Build(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<BuildingNotConfigured>(AssertFailure(report));
    }

    [Fact]
    public async Task ListBuildable_WhenBuildingIsConfigured_AsksTheBuildCommand()
    {
        // Arrange
        Pipeline pipeline = new(this.Specify(), Prelude.Some(this.Build()));

        // Act
        _ = await pipeline.ListBuildable(TestContext.Current.CancellationToken);

        // Assert
        _ = await this._work.Received(1).ListSpecified(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Build_WhenBuildingIsConfigured_BuildsTheItem()
    {
        // Arrange
        _ = this._work
            .Read(Seven, Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<WorkItem>(new WorkResponseMalformed("unreadable")));
        Pipeline pipeline = new(this.Specify(), Prelude.Some(this.Build()));

        // Act
        Fin<string> report = await pipeline.Build(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<WorkResponseMalformed>(AssertFailure(report));
    }

    [Fact]
    public async Task Run_WhenCalled_SpecifiesTheItem()
    {
        // Arrange
        _ = this._work
            .Read(Seven, Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<WorkItem>(new WorkResponseMalformed("unreadable")));
        Pipeline pipeline = new(this.Specify(), Option<BuildCommand>.None);

        // Act
        Fin<string> report = await pipeline.Run(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<WorkResponseMalformed>(AssertFailure(report));
    }

    [Fact]
    public async Task Revise_WhenCalled_RevisesTheItemsSpecification()
    {
        // Arrange
        _ = this._changes
            .ReadReview(Seven, Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<ProposalReview>(new ProposalNotFound(Seven)));
        Pipeline pipeline = new(this.Specify(), Option<BuildCommand>.None);

        // Act
        Fin<string> report = await pipeline.Revise(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<ProposalNotFound>(AssertFailure(report));
    }

    private GitRepository Git() => new(this._processes, "/repository", new GitIdentity("bot", "bot@example.com"));

    private SpecifyCommand Specify() =>
        new(new SpecifyStage(this._work, this._agent, this._gate), this._work, this.Git(), this._changes, "master");

    private BuildCommand Build() =>
        new(new BuildStage(this._agent, this._gate, this.Git()), this._work, this.Git(), this._changes, "master");
}