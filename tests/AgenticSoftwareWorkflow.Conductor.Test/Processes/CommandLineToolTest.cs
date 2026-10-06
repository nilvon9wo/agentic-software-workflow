using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Processes;

public sealed class CommandLineToolTest
{
    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();

    [Fact]
    public async Task Run_WhenTheToolSucceeds_ReturnsItsStandardOutput()
    {
        // Arrange
        this.Responds(new ProcessOutcome(0, "output", "", false));
        CommandLineTool git = new(this._processes, "git", "/repository");

        // Act
        Fin<string> result = await git.Run(["status"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("output", AssertSuccess(result));
    }

    [Fact]
    public async Task Run_WhenGivenInput_RunsTheToolWhereConfiguredWithThatInput()
    {
        // Arrange
        this.Responds(new ProcessOutcome(0, "", "", false));
        CommandLineTool gh = new(this._processes, "gh", "/repository");

        // Act
        _ = await gh.Run(["issue", "comment"], "body", TestContext.Current.CancellationToken);

        // Assert
        ProcessRequest request = (ProcessRequest)this._processes.ReceivedCalls().Single().GetArguments()[0]!;
        Assert.Equal(
            ("gh", "issue comment", "body", "/repository"),
            (request.Executable, string.Join(' ', request.Arguments), request.StandardInput, request.WorkingDirectory)
        );
    }

    [Fact]
    public async Task Run_WhenTheToolFails_NamesTheCommandAndItsError()
    {
        // Arrange
        this.Responds(new ProcessOutcome(128, "", " fatal: not a git repository\n", false));
        CommandLineTool git = new(this._processes, "git", "/repository");

        // Act
        Fin<string> result = await git.Run(["worktree", "add", "-b", "branch"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            new CommandFailed("git worktree add", 128, " fatal: not a git repository\n"),
            AssertFailure(result)
        );
    }

    [Fact]
    public async Task Run_WhenTheToolTimesOut_FailsSayingSo()
    {
        // Arrange
        this.Responds(new ProcessOutcome(-1, "", "", true));
        CommandLineTool git = new(this._processes, "git", "/repository");

        // Act
        Fin<string> result = await git.Run(["fetch"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("'git fetch' exited with code -1: timed out", AssertFailure(result).Message);
    }

    private void Responds(ProcessOutcome outcome) =>
        this._processes.Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>()).Returns(outcome);
}