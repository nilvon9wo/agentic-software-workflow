using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Claude;

namespace AgenticSoftwareWorkflow.Conductor.Test.Claude;

public sealed class ClaudeCapabilitiesTest
{
    [Theory]
    [InlineData(CapabilityTier.Small, "haiku")]
    [InlineData(CapabilityTier.Standard, "sonnet")]
    [InlineData(CapabilityTier.Strongest, "opus")]
    public void ModelFor_WhenGivenATier_ReturnsItsModelAlias(CapabilityTier tier, string expectedAlias)
    {
        // Arrange
        // Nothing to arrange: the inputs come from the [InlineData] rows.

        // Act
        string alias = ClaudeCapabilities.ModelFor(tier);

        // Assert
        Assert.Equal(expectedAlias, alias);
    }

    [Fact]
    public void ModelFor_WhenTheTierIsUndefined_Throws()
    {
        // Arrange
        CapabilityTier undefinedTier = (CapabilityTier)int.MaxValue;

        // Act
        ArgumentOutOfRangeException thrown =
            Assert.Throws<ArgumentOutOfRangeException>(() => ClaudeCapabilities.ModelFor(undefinedTier));

        // Assert
        Assert.Equal("tier", thrown.ParamName);
    }

    [Fact]
    public void ToolNamesFor_WhenGivenEveryCapability_ReturnsTheirToolsInOrder()
    {
        // Arrange
        AgentTool[] everyCapability =
            [AgentTool.ReadFiles, AgentTool.SearchFiles, AgentTool.EditFiles, AgentTool.RunCommands];

        // Act
        IReadOnlyList<string> toolNames = ClaudeCapabilities.ToolNamesFor(everyCapability);

        // Assert
        Assert.Equal(["Read", "Grep", "Glob", "Edit", "Write", "Bash"], toolNames);
    }

    [Fact]
    public void ToolNamesFor_WhenACapabilityIsUndefined_Throws()
    {
        // Arrange
        AgentTool[] undefinedCapability = [(AgentTool)int.MaxValue];

        // Act
        ArgumentOutOfRangeException thrown =
            Assert.Throws<ArgumentOutOfRangeException>(() => ClaudeCapabilities.ToolNamesFor(undefinedCapability));

        // Assert
        Assert.Equal("tool", thrown.ParamName);
    }
}