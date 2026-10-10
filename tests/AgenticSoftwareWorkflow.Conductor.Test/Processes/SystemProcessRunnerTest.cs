using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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

        // Act
        ProcessOutcome outcome = await this._runner.Run(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ProcessOutcome(0, "hello from stdin", "", false), outcome);
    }

    [Fact]
    public async Task Run_WhenTheProcessFails_CapturesItsExitCodeAndStandardError()
    {
        // Arrange
        ProcessRequest request = Request(["fail", "3"], "", Generous);

        // Act
        ProcessOutcome outcome = await this._runner.Run(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ProcessOutcome(3, "", "failed", false), outcome);
    }

    [Fact]
    public async Task Run_WhenTheProcessOutlivesItsTimeout_StopsItAndReportsTheTimeout()
    {
        // Arrange
        ProcessRequest request = Request(["hang"], "", Brief);

        // Act
        ProcessOutcome outcome = await this._runner.Run(request, TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task Run_WhenTheCallerCancels_StopsTheProcess()
    {
        // Arrange
        string processIdFile = Path.Combine(Path.GetTempPath(), $"aswf-hang-{Guid.NewGuid():N}");
        ProcessRequest request = Request(["hang", processIdFile], "", Generous);
        using CancellationTokenSource cancellation = new();
        Task cancelOnceStarted = CancelOnceWritten(processIdFile, cancellation);

        // Act
        _ = await Record.ExceptionAsync(() => this._runner.Run(request, cancellation.Token));

        // Assert
        await cancelOnceStarted;
        string processId = await File.ReadAllTextAsync(processIdFile, TestContext.Current.CancellationToken);
        File.Delete(processIdFile);
        Assert.False(IsRunning(int.Parse(processId, CultureInfo.InvariantCulture)));
    }

    private static ProcessRequest Request(string[] arguments, string standardInput, TimeSpan timeout) =>
        new(Dotnet, [TestProcess, .. arguments], standardInput, AppContext.BaseDirectory, timeout);

    // Cancels only once the child has started and said who it is, so the test
    // checks that a running process is stopped; gives up after a minute.
    private static async Task CancelOnceWritten(string processIdFile, CancellationTokenSource cancellation)
    {
        using CancellationTokenSource deadline = new(Generous);
        while (!HasContent(processIdFile))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), deadline.Token);
        }

        await cancellation.CancelAsync();
    }

    private static bool HasContent(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

    private static bool IsRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.WaitForExit(Generous);
        }
        catch (ArgumentException)
        {
            // No process has that id any more: it was stopped.
            return false;
        }
    }
}