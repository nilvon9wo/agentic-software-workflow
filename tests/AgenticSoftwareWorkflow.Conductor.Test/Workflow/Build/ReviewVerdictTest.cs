using AgenticSoftwareWorkflow.Conductor.Workflow.Build;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Build;

public sealed class ReviewVerdictTest
{
    [Fact]
    public void IsApproved_WhenTheVerdictIsApprove_IsTrue()
    {
        // Arrange
        ReviewVerdict verdict = new("approve", []);

        // Act
        bool isApproved = verdict.IsApproved;

        // Assert
        Assert.True(isApproved);
    }

    [Fact]
    public void IsApproved_WhenTheVerdictIsRevise_IsFalse()
    {
        // Arrange
        ReviewVerdict verdict = new("revise", ["Missing a test."]);

        // Act
        bool isApproved = verdict.IsApproved;

        // Assert
        Assert.False(isApproved);
    }

    [Fact]
    public void Describe_WhenThereAreNoFindings_IsEmpty()
    {
        // Arrange
        ReviewVerdict verdict = new("revise", null);

        // Act
        string described = verdict.Describe();

        // Assert
        Assert.Equal("", described);
    }

    [Fact]
    public void Schema_WhenRead_RequiresAVerdictAndFindings()
    {
        // Arrange
        // Nothing to arrange: the schema is a constant.

        // Act
        string schema = ReviewVerdict.Schema;

        // Assert
        Assert.Contains("\"required\": [\"verdict\", \"findings\"]", schema, StringComparison.Ordinal);
    }
}