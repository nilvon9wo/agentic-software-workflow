using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Specify;

public sealed class SpecificationFileNameTest
{
    private static readonly WorkItemId Eight = new("github:owner/repository", "8");

    [Fact]
    public void For_WhenGivenAnItem_NamesTheFileAfterItsKeyAndTitle()
    {
        // Arrange
        WorkItem item = Item("Extend the gates to Markdown and workflow files locally");

        // Act
        string fileName = SpecificationFileName.For(item);

        // Assert
        Assert.Equal("8-extend-the-gates-to-markdown-and-workflow-files-locally.md", fileName);
    }

    [Fact]
    public void For_WhenTheTitleHasPunctuation_UsesOneHyphenPerGap()
    {
        // Arrange
        WorkItem item = Item("  Fix: crash (on start-up)!  ");

        // Act
        string fileName = SpecificationFileName.For(item);

        // Assert
        Assert.Equal("8-fix-crash-on-start-up.md", fileName);
    }

    [Fact]
    public void For_WhenTheTitleIsLong_CutsItAtAWordBoundary()
    {
        // Arrange
        WorkItem item = Item(
            "The specifier's output must pass the documentation gates before it is proposed for review"
        );

        // Act
        string fileName = SpecificationFileName.For(item);

        // Assert
        Assert.Equal("8-the-specifier-s-output-must-pass-the-documentation-gates.md", fileName);
    }

    [Fact]
    public void For_WhenTheTitleIsOneLongWord_CutsItAtTheLimit()
    {
        // Arrange
        WorkItem item = Item(new string('a', 70));

        // Act
        string fileName = SpecificationFileName.For(item);

        // Assert
        Assert.Equal($"8-{new string('a', 60)}.md", fileName);
    }

    [Fact]
    public void For_WhenTheTitleHasNoLettersOrDigits_UsesTheKeyAlone()
    {
        // Arrange
        WorkItem item = Item("?!");

        // Act
        string fileName = SpecificationFileName.For(item);

        // Assert
        Assert.Equal("8.md", fileName);
    }

    private static WorkItem Item(string title) => new(Eight, title, "", [], [], false);
}