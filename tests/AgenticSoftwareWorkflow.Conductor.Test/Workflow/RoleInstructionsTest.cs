using AgenticSoftwareWorkflow.Conductor.Workflow;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow;

public sealed class RoleInstructionsTest
{
    [Fact]
    public void Specifier_WhenLoaded_IsTheEmbeddedSpecifierProcedure()
    {
        // Act
        string instructions = RoleInstructions.Specifier;

        // Assert
        Assert.StartsWith("# Specifier", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WhenTheInstructionsAreNotEmbedded_ThrowsNamingTheFileAndTheFix()
    {
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