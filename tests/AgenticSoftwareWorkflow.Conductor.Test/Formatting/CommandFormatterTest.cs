using AgenticSoftwareWorkflow.Conductor.Formatting;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Formatting;

public sealed class CommandFormatterTest
{
    private const string Workspace = "/repository/.aswf/worktrees/aswf-build-7";

    private static readonly IReadOnlyList<string> Command = ["dotnet", "format", "Project.slnx", "--severity", "info"];

    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();

    [Fact]
    public async Task Format_WhenCalled_RunsTheProjectsCommandInTheWorkspace()
    {
        // Arrange
        this.Finishes(new ProcessOutcome(0, "", "", false));
        IFormatting formatter = new CommandFormatter(this._processes, Command);

        // Act
        _ = await formatter.Format(Workspace, TestContext.Current.CancellationToken);

        // Assert
        ProcessRequest request = (ProcessRequest)this._processes.ReceivedCalls().Single().GetArguments()[0]!;
        Assert.Equal(
            ("dotnet", "format Project.slnx --severity info", Workspace),
            (request.Executable, string.Join(' ', request.Arguments), request.WorkingDirectory)
        );
    }

    [Fact]
    public async Task Format_WhenTheCommandSucceeds_Succeeds()
    {
        // Arrange
        this.Finishes(new ProcessOutcome(0, "", "", false));
        IFormatting formatter = new CommandFormatter(this._processes, Command);

        // Act
        Fin<Unit> formatted = await formatter.Format(Workspace, TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(formatted);
    }

    [Fact]
    public async Task Format_WhenTheCommandFails_FailsWithEverythingItPrinted()
    {
        // Arrange
        this.Finishes(new ProcessOutcome(1, "Formatting…\n", "Could not load Project.slnx\n", false));
        IFormatting formatter = new CommandFormatter(this._processes, Command);

        // Act
        Fin<Unit> formatted = await formatter.Format(Workspace, TestContext.Current.CancellationToken);

        // Assert
        FormattingFailed failed = Assert.IsType<FormattingFailed>(AssertFailure(formatted));
        Assert.Equal("Formatting…\nCould not load Project.slnx\n", failed.Report);
    }

    private void Finishes(ProcessOutcome outcome) =>
        this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(outcome);
}