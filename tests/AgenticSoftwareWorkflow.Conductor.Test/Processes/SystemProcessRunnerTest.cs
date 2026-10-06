using System.ComponentModel;
using AgenticSoftwareWorkflow.Conductor.Processes;

namespace AgenticSoftwareWorkflow.Conductor.Test.Processes;

/// <summary>
/// Runs real child processes: the AgenticSoftwareWorkflow.TestProcess helper,
/// built beside this test assembly, which echoes, fails, or hangs on request.
/// </summary>
public sealed class SystemProcessRunnerTest
{
    private const string Dotnet = "dotnet";

    private static readonly string TestProcess =
        Path.Combine(AppContext.BaseDirectory, "AgenticSoftwareWorkflow.TestProcess.dll");

    private static readonly TimeSpan Generous = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Brief = TimeSpan.FromMilliseconds(500);

    private readonly SystemProcessRunner _runner = new();

    [Fact]
    public async Task Run_WhenTheProcessSucceeds_CapturesItsOutput()
    {
        // Arrange
        ProcessRequest request = Request(["echo"], "hello from stdin", Generous);
        ProcessOutcome outcome;

        // Act
        outcome = await this._runner.Run(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ProcessOutcome(0, "hello from stdin", "", false), outcome);
    }

    [Fact]
    public async Task Run_WhenTheProcessFails_CapturesItsExitCodeAndStandardError()
    {
        // Arrange
        ProcessRequest request = Request(["fail", "3"], "", Generous);
        ProcessOutcome outcome;

        // Act
        outcome = await this._runner.Run(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ProcessOutcome(3, "", "failed", false), outcome);
    }

    [Fact]
    public async Task Run_WhenTheProcessOutlivesItsTimeout_StopsItAndReportsTheTimeout()
    {
        // Arrange
        ProcessRequest request = Request(["hang"], "", Brief);
        ProcessOutcome outcome;

        // Act
        outcome = await this._runner.Run(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ProcessOutcome(-1, "", "", true), outcome);
    }

    [Fact]
    public async Task Run_WhenTheCallerCancels_PropagatesTheCancellation()
    {
        // Arrange
        ProcessRequest request = Request(["hang"], "", Generous);
        using CancellationTokenSource cancellation = new(Brief);

        // Act
        Exception? thrown = await Record.ExceptionAsync(() => this._runner.Run(request, cancellation.Token));

        // Assert
        _ = Assert.IsType<OperationCanceledException>(thrown, exactMatch: false);
    }

    [Fact]
    public async Task Run_WhenTheExecutableDoesNotExist_ThrowsLoudly()
    {
        // Arrange
        ProcessRequest request = new("no-such-executable-anywhere", [], "", ".", Generous);

        // Act
        Exception? thrown = await Record.ExceptionAsync(
            () => this._runner.Run(request, TestContext.Current.CancellationToken)
        );

        // Assert
        _ = Assert.IsType<Win32Exception>(thrown);
    }

    private static ProcessRequest Request(string[] arguments, string standardInput, TimeSpan timeout) =>
        new(Dotnet, [TestProcess, .. arguments], standardInput, AppContext.BaseDirectory, timeout);
}