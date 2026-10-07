using AgenticSoftwareWorkflow.Cli;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Conductor.Test.Cli;

public sealed class CompositionTest
{
    private static readonly ConductorSettings Settings = new(
        "owner/repository",
        "master",
        ["maintainer"],
        new GitIdentity("repository-bot", "bot@example.com"),
        ["true"]
    );

    [Fact]
    public void CreateSpecifyCommand_WhenGivenSettings_WiresTheRealAdaptersWithoutRunningAnything()
    {
        // Arrange
        Composition composition = new();

        // Act
        SpecifyCommand command = composition.CreateSpecifyCommand(Settings, "/repository");

        // Assert
        Assert.NotNull(command);
    }

    [Fact]
    public void CreateRunLoop_WhenGivenSettings_WiresTheRealAdaptersWithoutRunningAnything()
    {
        // Arrange
        Composition composition = new();
        CommandContext context = new("/repository", TextWriter.Null, composition);

        // Act
        RunLoop loop = composition.CreateRunLoop(Settings, context, RunLoopOptions.Once);

        // Assert
        Assert.NotNull(loop);
    }

    [Fact]
    public async Task Main_WhenGivenNoArguments_ExitsWithAUsageError()
    {
        // Arrange
        // Nothing to arrange: running with no arguments is the scenario.

        // Act
        int exitCode = await Program.Main([]);

        // Assert
        Assert.Equal(CommandLine.UsageError, exitCode);
    }
}