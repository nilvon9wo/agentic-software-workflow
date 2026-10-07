using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Workflow;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow;

/// <summary>
/// Each test states one separation-of-authority guarantee the pipeline relies
/// on. If one fails, a role can do something it must never do.
/// </summary>
public sealed class WorkflowRolesTest
{
    private static readonly Dictionary<string, AgentRole> RolesByName = new()
    {
        [nameof(WorkflowRoles.TestReviewer)] = WorkflowRoles.TestReviewer,
        [nameof(WorkflowRoles.CodeReviewer)] = WorkflowRoles.CodeReviewer,
        [nameof(WorkflowRoles.Arbitrator)] = WorkflowRoles.Arbitrator,
        [nameof(WorkflowRoles.Specifier)] = WorkflowRoles.Specifier,
    };

    [Theory]
    [InlineData(nameof(WorkflowRoles.TestReviewer))]
    [InlineData(nameof(WorkflowRoles.CodeReviewer))]
    [InlineData(nameof(WorkflowRoles.Arbitrator))]
    [InlineData(nameof(WorkflowRoles.Specifier))]
    public void ReadOnlyRoles_WhenDefined_CannotEdit(string roleName)
    {
        // Arrange
        AgentRole judge = RolesByName[roleName];

        // Act
        bool canEdit = judge.CanEdit;

        // Assert
        Assert.False(canEdit, $"{roleName} must only read.");
    }

    [Fact]
    public void Implementer_WhenDefined_CannotReadTheHiddenTests()
    {
        // Arrange
        // Nothing to arrange: the role catalogue is static.

        // Act
        IReadOnlyList<string> unreadable = WorkflowRoles.Implementer.Access.UnreadablePaths;

        // Assert
        Assert.Contains(WorkspaceLayout.HiddenTests, unreadable);
    }

    [Fact]
    public void Implementer_WhenDefined_CannotChangeTheDefinitionOfSuccess()
    {
        // Arrange
        // Nothing to arrange: the role catalogue is static.

        // Act
        IReadOnlyList<string> uneditable = WorkflowRoles.Implementer.Access.UneditablePaths;

        // Assert
        Assert.Equal(
            [WorkspaceLayout.Specification, WorkspaceLayout.VisibleTests, WorkspaceLayout.HiddenTests],
            uneditable
        );
    }

    [Fact]
    public void Implementer_WhenDefined_RunsOnlyBuildTestAndFormat()
    {
        // Arrange
        // Nothing to arrange: the role catalogue is static.

        // Act
        IReadOnlyList<string> commands = WorkflowRoles.Implementer.Access.AllowedCommands;

        // Assert
        Assert.Equal(["dotnet build", "dotnet test", "dotnet format"], commands);
    }

    [Fact]
    public void TestAuthor_WhenDefined_CannotChangeTheSpecificationOrTheCode()
    {
        // Arrange
        // Nothing to arrange: the role catalogue is static.

        // Act
        IReadOnlyList<string> uneditable = WorkflowRoles.TestAuthor.Access.UneditablePaths;

        // Assert
        Assert.Equal([WorkspaceLayout.Specification, WorkspaceLayout.Source], uneditable);
    }

    [Fact]
    public void TestAuthor_WhenDefined_MayFormatItsOwnFiles()
    {
        // Arrange
        // Nothing to arrange: the role catalogue is static.

        // Act
        IReadOnlyList<string> commands = WorkflowRoles.TestAuthor.Access.AllowedCommands;

        // Assert
        Assert.Contains("dotnet format", commands);
    }

    [Fact]
    public void Triage_WhenDefined_IsCheapAndHasNoTools()
    {
        // Arrange
        // Nothing to arrange: the role catalogue is static.

        // Act
        AgentRole triage = WorkflowRoles.Triage;

        // Assert
        Assert.Equal((CapabilityTier.Small, 0), (triage.Tier, triage.Tools.Count));
    }

    [Fact]
    public void Arbitrator_WhenDefined_UsesTheStrongestTier()
    {
        // Arrange
        // Nothing to arrange: the role catalogue is static.

        // Act
        CapabilityTier tier = WorkflowRoles.Arbitrator.Tier;

        // Assert
        Assert.Equal(CapabilityTier.Strongest, tier);
    }
}