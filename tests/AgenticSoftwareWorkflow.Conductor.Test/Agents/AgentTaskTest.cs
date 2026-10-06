using AgenticSoftwareWorkflow.Conductor.Agents;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Test.Agents;

public sealed class AgentTaskTest
{
    private const string Prompt = "Review the change.";
    private const string WorkingDirectory = "/repository";
    private const string Schema = "{\"type\":\"object\"}";

    private static readonly AgentRole Reviewer = new(
        CapabilityTier.Standard,
        [AgentTool.ReadFiles],
        AgentAccess.ToolsOnly
    );
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    [Fact]
    public void Constructor_WhenThePromptIsBlank_Throws()
    {
        // Act
        ArgumentException thrown =
            Assert.Throws<ArgumentException>(() => new AgentTask(Reviewer, " ", WorkingDirectory, Timeout));

        // Assert
        Assert.Equal("prompt", thrown.ParamName);
    }

    [Fact]
    public void Constructor_WhenTheWorkingDirectoryIsBlank_Throws()
    {
        // Act
        ArgumentException thrown =
            Assert.Throws<ArgumentException>(() => new AgentTask(Reviewer, Prompt, "", Timeout));

        // Assert
        Assert.Equal("workingDirectory", thrown.ParamName);
    }

    [Fact]
    public void Constructor_WhenTheTimeoutIsNotPositive_Throws()
    {
        // Act
        ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AgentTask(Reviewer, Prompt, WorkingDirectory, TimeSpan.Zero)
        );

        // Assert
        Assert.Equal("timeout", thrown.ParamName);
    }

    [Fact]
    public void Constructor_WhenGivenNoSchema_HasNoOutputSchema()
    {
        // Act
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Assert
        Assert.Equal(Option<string>.None, task.OutputSchema);
    }

    [Fact]
    public void WithOutputSchema_WhenGivenASchema_CarriesItAndEverythingElse()
    {
        // Arrange
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Act
        AgentTask withSchema = task.WithOutputSchema(Schema);

        // Assert
        Assert.Equal(
            (Reviewer, Prompt, WorkingDirectory, Timeout, Prelude.Some(Schema)),
            (
                withSchema.Role,
                withSchema.Prompt,
                withSchema.WorkingDirectory,
                withSchema.Timeout,
                withSchema.OutputSchema
            )
        );
    }

    [Fact]
    public void WithOutputSchema_WhenCalled_LeavesTheOriginalUnchanged()
    {
        // Arrange
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Act
        _ = task.WithOutputSchema(Schema);

        // Assert
        Assert.Equal(Option<string>.None, task.OutputSchema);
    }

    [Fact]
    public void WithOutputSchema_WhenTheSchemaIsBlank_Throws()
    {
        // Arrange
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Act
        ArgumentException thrown = Assert.Throws<ArgumentException>(() => task.WithOutputSchema(""));

        // Assert
        Assert.Equal("jsonSchema", thrown.ParamName);
    }

    [Fact]
    public void WithInstructions_WhenGivenInstructions_CarriesThemAndKeepsTheSchema()
    {
        // Arrange
        AgentTask task = new AgentTask(Reviewer, Prompt, WorkingDirectory, Timeout).WithOutputSchema(Schema);

        // Act
        AgentTask instructed = task.WithInstructions("Review carefully.");

        // Assert
        Assert.Equal(
            (Prelude.Some("Review carefully."), Prelude.Some(Schema)),
            (instructed.Instructions, instructed.OutputSchema)
        );
    }

    [Fact]
    public void WithOutputSchema_WhenTheTaskHasInstructions_KeepsThem()
    {
        // Arrange
        AgentTask task = new AgentTask(Reviewer, Prompt, WorkingDirectory, Timeout).WithInstructions("Review.");

        // Act
        AgentTask withSchema = task.WithOutputSchema(Schema);

        // Assert
        Assert.Equal(Prelude.Some("Review."), withSchema.Instructions);
    }

    [Fact]
    public void WithInstructions_WhenTheInstructionsAreBlank_Throws()
    {
        // Arrange
        AgentTask task = new(Reviewer, Prompt, WorkingDirectory, Timeout);

        // Act
        ArgumentException thrown = Assert.Throws<ArgumentException>(() => task.WithInstructions(" "));

        // Assert
        Assert.Equal("instructions", thrown.ParamName);
    }
}