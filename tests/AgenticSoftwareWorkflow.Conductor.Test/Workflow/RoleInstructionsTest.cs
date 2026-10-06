using AgenticSoftwareWorkflow.Conductor.Workflow;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow;

public sealed class RoleInstructionsTest
{
    [Fact]
    public void Specifier_WhenLoaded_IsTheEmbeddedSpecifierProcedure()
    {
        // Arrange
        // Nothing to arrange: the instructions are embedded at build time.

        // Act
        string instructions = RoleInstructions.Specifier;

        // Assert
        Assert.StartsWith("# Specifier", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WhenTheInstructionsAreNotEmbedded_ThrowsNamingTheFileAndTheFix()
    {
        // Arrange
        // Nothing to arrange: the input is a literal in the Act.

        // Act
        InvalidOperationException thrown =
            Assert.Throws<InvalidOperationException>(() => RoleInstructions.Load("missing.md"));

        // Assert
        Assert.Equal(
            "Role instructions 'missing.md' are not embedded; check the conductor's csproj.",
            thrown.Message
        );
    }
}