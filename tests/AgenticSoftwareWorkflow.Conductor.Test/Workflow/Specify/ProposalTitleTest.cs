using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Specify;

public sealed class ProposalTitleTest
{
    private static readonly WorkItemId TwentyFive = new("github:owner/repository", "25");

    [Fact]
    public void For_WhenTheSpecificationHasAHeading_NamesTheIntentAndTheKey()
    {
        // Arrange
        const string specification = "# Parameterise tests that differ only by data\n\n## Summary\n";

        // Act
        string title = ProposalTitle.For(TwentyFive, specification);

        // Assert
        Assert.Equal("Specify: Parameterise tests that differ only by data (#25)", title);
    }

    [Fact]
    public void For_WhenTheSpecificationHasNoHeading_FallsBackToTheItem()
    {
        // Arrange
        const string specification = "## Summary\n\nNo top-level heading.\n";

        // Act
        string title = ProposalTitle.For(TwentyFive, specification);

        // Assert
        Assert.Equal("Specify: the work item github:owner/repository#25 (#25)", title);
    }

    [Fact]
    public void For_WhenTheHeadingIsVeryLong_ShortensIt()
    {
        // Arrange
        string specification = $"# {new string('a', 150)}\n";

        // Act
        string title = ProposalTitle.For(TwentyFive, specification);

        // Assert
        Assert.Equal($"Specify: {new string('a', 100)}… (#25)", title);
    }
}