using AgenticSoftwareWorkflow.Conductor.Claude;

namespace AgenticSoftwareWorkflow.Conductor.Test.Claude;

public sealed class ClaudeInvocationTest
{
    private const string Model = "sonnet";

    [Fact]
    public void Headless_WhenGivenAModel_ProducesANonInteractiveJsonRun()
    {
        // Arrange
        IReadOnlyList<string> expectedArguments =
            [
                "--print",
                "--output-format",
                "json",
                "--no-session-persistence",
                "--strict-mcp-config",
                "--setting-sources",
                "project",
                "--model",
                Model,
            ];

        // Act
        ClaudeInvocation invocation = ClaudeInvocation.Headless(Model);

        // Assert
        Assert.Equal(expectedArguments, invocation.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Headless_WhenTheModelIsBlank_Throws(string blankModel)
    {
        // Act
        ArgumentException thrown =
            Assert.Throws<ArgumentException>(() => ClaudeInvocation.Headless(blankModel));

        // Assert
        Assert.Equal("model", thrown.ParamName);
    }

    [Fact]
    public void WithTools_WhenGivenToolNames_AppendsThemCommaSeparated()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ClaudeInvocation invocation = baseInvocation.WithTools(["Read", "Grep"]);

        // Assert
        Assert.Equal(["--tools", "Read,Grep"], invocation.Arguments.TakeLast(2));
    }

    [Fact]
    public void WithTools_WhenGivenNoToolNames_Throws()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ArgumentOutOfRangeException thrown =
            Assert.Throws<ArgumentOutOfRangeException>(() => baseInvocation.WithTools([]));

        // Assert
        Assert.Equal("toolNames", thrown.ParamName);
    }

    [Fact]
    public void WithoutTools_WhenCalled_AppendsAnEmptyToolList()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ClaudeInvocation invocation = baseInvocation.WithoutTools();

        // Assert
        Assert.Equal(["--tools", ""], invocation.Arguments.TakeLast(2));
    }

    [Theory]
    [InlineData(ClaudePermissionMode.Plan, "plan")]
    [InlineData(ClaudePermissionMode.AcceptEdits, "acceptEdits")]
    [InlineData(ClaudePermissionMode.DontAsk, "dontAsk")]
    public void WithPermissionMode_WhenGivenAMode_AppendsItsCliName(
        ClaudePermissionMode permissionMode,
        string expectedArgument
    )
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ClaudeInvocation invocation = baseInvocation.WithPermissionMode(permissionMode);

        // Assert
        Assert.Equal(["--permission-mode", expectedArgument], invocation.Arguments.TakeLast(2));
    }

    [Fact]
    public void WithPermissionMode_WhenTheModeIsUndefined_Throws()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);
        ClaudePermissionMode undefinedMode = (ClaudePermissionMode)int.MaxValue;

        // Act
        ArgumentOutOfRangeException thrown =
            Assert.Throws<ArgumentOutOfRangeException>(() => baseInvocation.WithPermissionMode(undefinedMode));

        // Assert
        Assert.Equal("permissionMode", thrown.ParamName);
    }

    [Fact]
    public void WithSettings_WhenGivenAPath_AppendsIt()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ClaudeInvocation invocation = baseInvocation.WithSettings("roles/reviewer.json");

        // Assert
        Assert.Equal(["--settings", "roles/reviewer.json"], invocation.Arguments.TakeLast(2));
    }

    [Fact]
    public void WithSettings_WhenThePathIsBlank_Throws()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ArgumentException thrown =
            Assert.Throws<ArgumentException>(() => baseInvocation.WithSettings(" "));

        // Assert
        Assert.Equal("settingsFileOrJson", thrown.ParamName);
    }

    [Fact]
    public void WithAppendedSystemPrompt_WhenGivenInstructions_AppendsThem()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ClaudeInvocation invocation = baseInvocation.WithAppendedSystemPrompt("Review carefully.");

        // Assert
        Assert.Equal(["--append-system-prompt", "Review carefully."], invocation.Arguments.TakeLast(2));
    }

    [Fact]
    public void WithAppendedSystemPrompt_WhenTheInstructionsAreBlank_Throws()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ArgumentException thrown =
            Assert.Throws<ArgumentException>(() => baseInvocation.WithAppendedSystemPrompt(""));

        // Assert
        Assert.Equal("instructions", thrown.ParamName);
    }

    [Fact]
    public void WithJsonSchema_WhenGivenASchema_AppendsIt()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ClaudeInvocation invocation = baseInvocation.WithJsonSchema("{\"type\":\"object\"}");

        // Assert
        Assert.Equal(["--json-schema", "{\"type\":\"object\"}"], invocation.Arguments.TakeLast(2));
    }

    [Fact]
    public void WithJsonSchema_WhenTheSchemaIsBlank_Throws()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);

        // Act
        ArgumentException thrown =
            Assert.Throws<ArgumentException>(() => baseInvocation.WithJsonSchema(""));

        // Assert
        Assert.Equal("jsonSchema", thrown.ParamName);
    }

    [Fact]
    public void WithTools_WhenCalledOnASharedBase_LeavesTheBaseUnchanged()
    {
        // Arrange
        ClaudeInvocation baseInvocation = ClaudeInvocation.Headless(Model);
        IReadOnlyList<string> baseArguments = [.. baseInvocation.Arguments];

        // Act
        _ = baseInvocation.WithTools(["Edit"]);

        // Assert
        Assert.Equal(baseArguments, baseInvocation.Arguments);
    }

    [Fact]
    public void Arguments_WhenARoleIsComposed_ReadAsTheFullCommandLine()
    {
        // Arrange
        IReadOnlyList<string> expectedArguments =
            [
                "--print",
                "--output-format",
                "json",
                "--no-session-persistence",
                "--strict-mcp-config",
                "--setting-sources",
                "project",
                "--model",
                "sonnet",
                "--tools",
                "Read,Grep,Glob",
                "--permission-mode",
                "dontAsk",
                "--settings",
                ".claude/roles/code-reviewer.json",
            ];

        // Act
        // begin-snippet: compose-reviewer-invocation
        ClaudeInvocation reviewer = ClaudeInvocation.Headless("sonnet")
            .WithTools(["Read", "Grep", "Glob"])
            .WithPermissionMode(ClaudePermissionMode.DontAsk)
            .WithSettings(".claude/roles/code-reviewer.json");
        // end-snippet

        // Assert
        Assert.Equal(expectedArguments, reviewer.Arguments);
    }
}