using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Claude;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Test.Claude;

public sealed class ClaudePermissionSettingsTest
{
    [Fact]
    public void For_WhenTheRoleHasNoAccessRules_ProducesNoSettings()
    {
        // Arrange
        // Nothing to arrange: the input is the predefined ToolsOnly access.

        // Act
        Option<string> settings = ClaudePermissionSettings.For(AgentAccess.ToolsOnly);

        // Assert
        Assert.Equal(Option<string>.None, settings);
    }

    [Fact]
    public void For_WhenTheRoleHasAccessRules_DeniesPathsAndAllowsOnlyItsCommands()
    {
        // Arrange
        AgentAccess access = new(["hidden-tests/**"], ["spec/**"], ["dotnet test"]);
        string expectedSettings = "{\"permissions\":{\"allow\":[\"Bash(dotnet test:*)\"],"
            + "\"deny\":[\"Read(./hidden-tests/**)\",\"Edit(./spec/**)\"]}}";

        // Act
        Option<string> settings = ClaudePermissionSettings.For(access);

        // Assert
        Assert.Equal(Prelude.Some(expectedSettings), settings);
    }

    [Fact]
    public void For_WhenTheRoleOnlyAllowsCommands_StillProducesSettings()
    {
        // Arrange
        AgentAccess access = new([], [], ["dotnet build"]);

        // Act
        Option<string> settings = ClaudePermissionSettings.For(access);

        // Assert
        Assert.Equal(Prelude.Some("""{"permissions":{"allow":["Bash(dotnet build:*)"],"deny":[]}}"""), settings);
    }
}