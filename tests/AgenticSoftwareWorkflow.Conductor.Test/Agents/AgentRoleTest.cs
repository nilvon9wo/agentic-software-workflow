using AgenticSoftwareWorkflow.Conductor.Agents;

namespace AgenticSoftwareWorkflow.Conductor.Test.Agents;

public sealed class AgentRoleTest
{
    [Fact]
    public void CanEdit_WhenGrantedEditFiles_IsTrue()
    {
        // Arrange
        AgentRole implementer = new(
            CapabilityTier.Standard,
            [AgentTool.ReadFiles, AgentTool.EditFiles],
            AgentAccess.ToolsOnly
        );

        // Act
        bool canEdit = implementer.CanEdit;

        // Assert
        Assert.True(canEdit);
    }

    [Fact]
    public void CanEdit_WhenNotGrantedEditFiles_IsFalse()
    {
        // Arrange
        AgentRole reviewer = new(
            CapabilityTier.Standard,
            [AgentTool.ReadFiles, AgentTool.SearchFiles],
            AgentAccess.ToolsOnly
        );

        // Act
        bool canEdit = reviewer.CanEdit;

        // Assert
        Assert.False(canEdit);
    }
}