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
        Option<string> settings;

        // Act
        settings = ClaudePermissionSettings.For(AgentAccess.ToolsOnly);

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
        Option<string> settings;

        // Act
        settings = ClaudePermissionSettings.For(access);

        // Assert
        Assert.Equal(Prelude.Some(expectedSettings), settings);
    }

    [Fact]
    public void For_WhenTheRoleOnlyAllowsCommands_StillProducesSettings()
    {
        // Arrange
        AgentAccess access = new([], [], ["dotnet build"]);
        Option<string> settings;

        // Act
        settings = ClaudePermissionSettings.For(access);

        // Assert
        Assert.Equal(Prelude.Some("""{"permissions":{"allow":["Bash(dotnet build:*)"],"deny":[]}}"""), settings);
    }
}