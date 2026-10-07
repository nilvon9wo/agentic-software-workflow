using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using LanguageExt;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow;

public sealed class RunLoopTest : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Reset = Start.AddHours(2);
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(10);
    private static readonly WorkItemId Seven = new("github:owner/repository", "7");
    private static readonly WorkItemId Eight = new("github:owner/repository", "8");
    private static readonly UsageLimitReached FiveHourLimit = new("five_hour", Prelude.Some(Reset));

    private readonly IWorkSupplying _work = Substitute.For<IWorkSupplying>();
    private readonly IWorkProcessing _processing = Substitute.For<IWorkProcessing>();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly StringWriter _log = new();
    private readonly CancellationTokenSource _stopping = new();

    public RunLoopTest()
    {
        this.Ready(Seven);
        _ = this._processing
            .Run(Arg.Any<WorkItemId>(), Arg.Any<CancellationToken>())
            .Returns(call => Fin.Succ($"Did {call.Arg<WorkItemId>()}."));
        _ = this._processing
            .ListAwaitingRevision(Arg.Any<CancellationToken>())
            .Returns(Fin.Succ<IReadOnlyList<WorkItemId>>([]));
        _ = this._processing
            .UpdateBehindProposals(Arg.Any<CancellationToken>())
            .Returns(Fin.Succ<IReadOnlyList<string>>([]));
        _ = this._processing
            .Revise(Arg.Any<WorkItemId>(), Arg.Any<CancellationToken>())
            .Returns(call => Fin.Succ($"Revised {call.Arg<WorkItemId>()}."));
        _ = this._work
            .Ask(Arg.Any<WorkItemId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Succ(Unit.Default));
    }

    [Fact]
    public async Task Run_WhenRunningOnce_ProcessesEachReadyItemInOrder()
    {
        // Arrange
        this.Ready(Seven, Eight);
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        Received.InOrder(
            () =>
            {
                _ = this._processing.Run(Seven, Arg.Any<CancellationToken>());
                _ = this._processing.Run(Eight, Arg.Any<CancellationToken>());
            }
        );
    }

    [Fact]
    public async Task Run_WhenAnItemSucceeds_LogsWhatHappenedWithTheTime()
    {
        // Arrange
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["[2026-10-07 12:00:00Z] 1 item(s) ready.", "[2026-10-07 12:00:00Z] Did github:owner/repository#7."],
            this.LogLines()
        );
    }

    [Fact]
    public async Task Run_WhenTheReadyItemsCannotBeListed_LogsWhyAndProcessesNothing()
    {
        // Arrange
        _ = this._work
            .ListReady(Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<IReadOnlyList<WorkItemId>>(new CommandFailed("gh issue list", 1, "offline")));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            (
                "[2026-10-07 12:00:00Z] Could not list the ready items: 'gh issue list' exited with code 1: offline",
                0
            ),
            (Assert.Single(this.LogLines()), this.RunCalls())
        );
    }

    [Fact]
    public async Task Run_WhenAnItemFails_PutsTheFailureToAMaintainerOnTheItem()
    {
        // Arrange
        this.Processing(Fin.Fail<string>(new AgentTimedOut(TimeSpan.FromMinutes(15))));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        _ = await this._work.Received(1).Ask(
            Seven,
            Arg.Is<string>(question => question.Contains("The agent did not finish within 00:15:00.")),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Run_WhenAskingAboutAFailureAlsoFails_LogsBoth()
    {
        // Arrange
        this.Processing(Fin.Fail<string>(new AgentTimedOut(TimeSpan.FromMinutes(15))));
        _ = this._work
            .Ask(Arg.Any<WorkItemId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<Unit>(new CommandFailed("gh issue comment", 1, "offline")));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "[2026-10-07 12:00:00Z] 1 item(s) ready.",
                "[2026-10-07 12:00:00Z] github:owner/repository#7 failed: The agent did not finish within 00:15:00.",
                "[2026-10-07 12:00:00Z] Could not ask a maintainer about github:owner/repository#7 either: "
                + "'gh issue comment' exited with code 1: offline",
            ],
            this.LogLines()
        );
    }

    [Fact]
    public void Run_WhenTheUsageLimitIsReached_WaitsUntilAMinuteAfterTheReset()
    {
        // Arrange
        this.Processing(Fin.Fail<string>(FiveHourLimit));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        _ = loop.Run(this._stopping.Token);

        // Assert
        Assert.EndsWith(
            "it resets at 2026-10-07 14:00:00Z. Waiting until 2026-10-07 14:01:00Z, then retrying "
            + "github:owner/repository#7.",
            this.LogLines()[^1],
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Run_WhenTheUsageLimitHasReset_RetriesTheSameItem()
    {
        // Arrange
        this.Processing(Fin.Fail<string>(FiveHourLimit), Fin.Succ("Done."));
        RunLoop loop = this.Loop(RunLoopOptions.Once);
        Task running = loop.Run(TestContext.Current.CancellationToken);

        // Act
        this._time.Advance(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(1));

        // Assert
        await running.WaitAsync(Settle, TestContext.Current.CancellationToken);
        _ = await this._processing.Received(2).Run(Seven, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Run_WhenTheUsageLimitGivesNoResetTime_WaitsAnHour()
    {
        // Arrange
        this.Processing(Fin.Fail<string>(new UsageLimitReached("five_hour", Option<DateTimeOffset>.None)));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        _ = loop.Run(this._stopping.Token);

        // Assert
        Assert.EndsWith(
            "Waiting until 2026-10-07 13:00:00Z, then retrying github:owner/repository#7.",
            this.LogLines()[^1],
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Run_WhenTheResetHasAlreadyPassed_RetriesAtOnce()
    {
        // Arrange
        UsageLimitReached alreadyReset = new("five_hour", Prelude.Some(Start.AddHours(-1)));
        this.Processing(Fin.Fail<string>(alreadyReset), Fin.Succ("Done."));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        _ = await this._processing.Received(2).Run(Seven, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WhenRunningContinuously_PassesAgainAfterTheInterval()
    {
        // Arrange
        TaskCompletionSource secondPass = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Fin<IReadOnlyList<WorkItemId>> nothingReady = Fin.Succ<IReadOnlyList<WorkItemId>>([]);
        _ = this._work
            .ListReady(Arg.Any<CancellationToken>())
            .Returns(
                _ => nothingReady,
                call =>
                {
                    _ = secondPass.TrySetResult();
                    return nothingReady;
                }
            );
        _ = this.Loop(RunLoopOptions.Continuous).Run(this._stopping.Token);

        // Act
        this._time.Advance(TimeSpan.FromMinutes(10));

        // Assert
        await secondPass.Task.WaitAsync(Settle, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Run_WhenAProposalAwaitsRevision_RevisesItBeforeTheReadyItems()
    {
        // Arrange
        _ = this._processing
            .ListAwaitingRevision(Arg.Any<CancellationToken>())
            .Returns(Fin.Succ<IReadOnlyList<WorkItemId>>([Eight]));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        Received.InOrder(
            () =>
            {
                _ = this._processing.Revise(Eight, Arg.Any<CancellationToken>());
                _ = this._processing.Run(Seven, Arg.Any<CancellationToken>());
            }
        );
    }

    [Fact]
    public async Task Run_WhenTheProposalsCannotBeListed_LogsWhyAndStillProcessesTheReadyItems()
    {
        // Arrange
        _ = this._processing
            .ListAwaitingRevision(Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<IReadOnlyList<WorkItemId>>(new CommandFailed("gh pr list", 1, "offline")));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            (
                "[2026-10-07 12:00:00Z] Could not list the proposals awaiting revision: "
                + "'gh pr list' exited with code 1: offline",
                1
            ),
            (this.LogLines()[0], this.RunCalls())
        );
    }

    [Fact]
    public async Task Run_WhenProposalsWereBroughtUpToDate_LogsEachOneFirst()
    {
        // Arrange
        _ = this._processing
            .UpdateBehindProposals(Arg.Any<CancellationToken>())
            .Returns(Fin.Succ<IReadOnlyList<string>>(["Brought PR 9 up to date with master."]));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("[2026-10-07 12:00:00Z] Brought PR 9 up to date with master.", this.LogLines()[0]);
    }

    [Fact]
    public async Task Run_WhenTheProposalsCannotBeCheckedForUpdates_LogsWhyAndCarriesOn()
    {
        // Arrange
        _ = this._processing
            .UpdateBehindProposals(Arg.Any<CancellationToken>())
            .Returns(Fin.Fail<IReadOnlyList<string>>(new CommandFailed("gh pr list", 1, "offline")));
        RunLoop loop = this.Loop(RunLoopOptions.Once);

        // Act
        await loop.Run(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            (
                "[2026-10-07 12:00:00Z] Could not check the proposals are up to date: "
                + "'gh pr list' exited with code 1: offline",
                1
            ),
            (this.LogLines()[0], this.RunCalls())
        );
    }

    public void Dispose()
    {
        this._stopping.Cancel();
        this._stopping.Dispose();
        this._log.Dispose();
    }

    private RunLoop Loop(RunLoopOptions options) => new(this._work, this._processing, options, this._log, this._time);

    private void Ready(params WorkItemId[] ids) =>
        this._work
            .ListReady(Arg.Any<CancellationToken>())
            .Returns(Fin.Succ<IReadOnlyList<WorkItemId>>(ids));

    private void Processing(Fin<string> first, params Fin<string>[] rest) =>
        this._processing
            .Run(Arg.Any<WorkItemId>(), Arg.Any<CancellationToken>())
            .Returns(first, rest);

    private List<string> LogLines() =>
        [.. this._log.ToString().ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    private int RunCalls() =>
        this._processing
            .ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IWorkProcessing.Run));
}