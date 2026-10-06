using AgenticSoftwareWorkflow.Cli;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Conductor.Test.Cli;

public sealed class CompositionTest
{
    [Fact]
    public void CreateSpecifyCommand_WhenGivenSettings_WiresTheRealAdaptersWithoutRunningAnything()
    {
        // Arrange
        ConductorSettings settings = new(
            "owner/repository",
            "master",
            ["maintainer"],
            new GitIdentity("repository-bot", "bot@example.com")
        );
        SpecifyCommand command;

        // Act
        command = Composition.CreateSpecifyCommand(settings, "/repository");

        // Assert
        Assert.NotNull(command);
    }

    [Fact]
    public async Task Main_WhenGivenNoArguments_ExitsWithAUsageError()
    {
        // Arrange
        int exitCode;

        // Act
        exitCode = await Program.Main([]);

        // Assert
        Assert.Equal(CommandLine.UsageError, exitCode);
    }
}