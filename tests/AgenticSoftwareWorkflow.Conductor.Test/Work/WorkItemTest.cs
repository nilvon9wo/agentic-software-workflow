using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Test.Work;

public sealed class WorkItemTest
{
    private static readonly WorkItemId Id = new("github:owner/repository", "7");
    private static readonly WorkComment Question = new("workers-bot", "Which zone?", false, true);
    private static readonly WorkComment Answer = new("maintainer", "Use UTC.", true, false);
    private static readonly WorkComment Stranger = new("stranger", "Use local time.", false, false);

    [Fact]
    public void IsAnswered_WhenAMaintainerRepliedAfterTheQuestion_IsTrue()
    {
        // Arrange
        WorkItem item = new(Id, "Title", "Body", [], [Question, Answer], true);

        // Act
        bool isAnswered = item.IsAnswered;

        // Assert
        Assert.True(isAnswered);
    }

    [Fact]
    public void IsAnswered_WhenTheWorkflowAskedAgainAfterTheReply_IsFalse()
    {
        // Arrange
        WorkItem item = new(Id, "Title", "Body", [], [Question, Answer, Question], true);

        // Act
        bool isAnswered = item.IsAnswered;

        // Assert
        Assert.False(isAnswered);
    }

    [Fact]
    public void IsAnswered_WhenOnlyAStrangerRepliedAfterTheQuestion_IsFalse()
    {
        // Arrange
        WorkItem item = new(Id, "Title", "Body", [], [Question, Stranger], true);

        // Act
        bool isAnswered = item.IsAnswered;

        // Assert
        Assert.False(isAnswered);
    }

    [Fact]
    public void IsAnswered_WhenTheItemIsNotWaiting_IsFalse()
    {
        // Arrange
        WorkItem item = new(Id, "Title", "Body", [], [Question, Answer], false);

        // Act
        bool isAnswered = item.IsAnswered;

        // Assert
        Assert.False(isAnswered);
    }

    [Fact]
    public void Conversation_WhenAStrangerCommented_KeepsOnlyTheWorkflowsAndMaintainersComments()
    {
        // Arrange
        WorkItem item = new(Id, "Title", "Body", [], [Question, Stranger, Answer], false);

        // Act
        IReadOnlyList<WorkComment> conversation = item.Conversation;

        // Assert
        Assert.Equal([Question, Answer], conversation);
    }

    [Fact]
    public void Parse_WhenGivenWhatToStringWrote_ReturnsTheSameId()
    {
        // Arrange
        string written = Id.ToString();

        // Act
        Option<WorkItemId> parsed = WorkItemId.Parse(written);

        // Assert
        Assert.Equal(Prelude.Some(Id), parsed);
    }

    [Theory]
    [InlineData("no-separator")]
    [InlineData("#7")]
    [InlineData("github:owner/repository#")]
    public void Parse_WhenTheTextLacksASourceOrKey_ReturnsNone(string text)
    {
        // Arrange
        // Nothing to arrange: the [InlineData] rows are the input.

        // Act
        Option<WorkItemId> parsed = WorkItemId.Parse(text);

        // Assert
        Assert.True(parsed.IsNone);
    }

    [Fact]
    public void ToString_WhenCalledOnAnId_ShowsItsSourceAndKey()
    {
        // Arrange
        // Nothing to arrange: the inputs are the class's shared constants.

        // Act
        string text = Id.ToString();

        // Assert
        Assert.Equal("github:owner/repository#7", text);
    }
}