using AgenticSoftwareWorkflow.Cli;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Build;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Cli;

public sealed class CompositionTest
{
    private static readonly ConductorSettings Settings = new(
        "owner/repository",
        "master",
        ["maintainer"],
        new GitIdentity("repository-bot", "bot@example.com"),
        ["true"],
        null,
        null
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

    // The formatter's length in words: none at all, an empty command, a command.
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CreatePipeline_WhenTheSettingsNameACodeGate_CanBuildWithOrWithoutAFormatter(int? formatterWords)
    {
        // Arrange
        DirectoryInfo notARepository = Directory.CreateTempSubdirectory("aswf-composition-");
        List<string>? formatter = formatterWords is { } words ? [.. Enumerable.Repeat("true", words)] : null;
        ConductorSettings settings = Settings with { CodeGate = ["false"], Formatter = formatter };
        Pipeline pipeline = new Composition().CreatePipeline(settings, notARepository.FullName);

        // Act
        Fin<string> report = await pipeline.Build(
            new WorkItemId("github:owner/repository", "7"),
            TestContext.Current.CancellationToken
        );

        // Assert
        notARepository.Delete(recursive: true);
        Assert.IsNotType<BuildingNotConfigured>(AssertFailure(report));
    }

    [Fact]
    public async Task CreatePipeline_WhenTheCodeGateIsEmpty_BuildsNothing()
    {
        // Arrange
        Pipeline pipeline = new Composition().CreatePipeline(Settings with { CodeGate = [] }, "/repository");

        // Act
        Fin<string> report = await pipeline.Build(
            new WorkItemId("github:owner/repository", "7"),
            TestContext.Current.CancellationToken
        );

        // Assert
        _ = Assert.IsType<BuildingNotConfigured>(AssertFailure(report));
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