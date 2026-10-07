using AgenticSoftwareWorkflow.Conductor.Gates;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Gates;

public sealed class CommandGateTest
{
    private const string Workspace = "/repository/.aswf/worktrees/aswf-specify-7";

    private static readonly IReadOnlyList<string> Command = ["bash", "scripts/gates.sh", "run", "markdownlint"];

    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();

    [Fact]
    public async Task Check_WhenCalled_RunsTheProjectsCommandInTheWorkspace()
    {
        // Arrange
        this.Finishes(new ProcessOutcome(0, "", "", false));
        IGateKeeping gate = new CommandGate(this._processes, Command);

        // Act
        _ = await gate.Check(Workspace, TestContext.Current.CancellationToken);

        // Assert
        ProcessRequest request = (ProcessRequest)this._processes.ReceivedCalls().Single().GetArguments()[0]!;
        Assert.Equal(
            ("bash", "scripts/gates.sh run markdownlint", Workspace),
            (request.Executable, string.Join(' ', request.Arguments), request.WorkingDirectory)
        );
    }

    [Fact]
    public async Task Check_WhenTheCommandSucceeds_Passes()
    {
        // Arrange
        this.Finishes(new ProcessOutcome(0, "[PASS] markdownlint", "", false));
        IGateKeeping gate = new CommandGate(this._processes, Command);

        // Act
        Fin<Unit> checkedByGate = await gate.Check(Workspace, TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(checkedByGate);
    }

    [Fact]
    public async Task Check_WhenTheCommandFails_FailsWithEverythingItPrinted()
    {
        // Arrange
        this.Finishes(new ProcessOutcome(1, "[FAIL] markdownlint\n", "spec/7.md:3 MD013\n", false));
        IGateKeeping gate = new CommandGate(this._processes, Command);

        // Act
        Fin<Unit> checkedByGate = await gate.Check(Workspace, TestContext.Current.CancellationToken);

        // Assert
        GatesFailed failed = Assert.IsType<GatesFailed>(AssertFailure(checkedByGate));
        Assert.Equal("[FAIL] markdownlint\nspec/7.md:3 MD013\n", failed.Report);
    }

    [Fact]
    public async Task Check_WhenTheCommandTimesOut_Fails()
    {
        // Arrange
        this.Finishes(new ProcessOutcome(0, "", "", true));
        IGateKeeping gate = new CommandGate(this._processes, Command);

        // Act
        Fin<Unit> checkedByGate = await gate.Check(Workspace, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<GatesFailed>(AssertFailure(checkedByGate));
    }

    private void Finishes(ProcessOutcome outcome) =>
        this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(outcome);
}