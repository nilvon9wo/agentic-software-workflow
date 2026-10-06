using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Test.Work;

public sealed class WorkErrorsTest
{
    [Fact]
    public void MalformedResponse_WhenCreated_CarriesItsCodeAndDetail()
    {
        // Arrange
        Error error;

        // Act
        error = WorkErrors.MalformedResponse("unexpected token");

        // Assert
        Assert.Equal(
            (WorkErrors.MalformedResponseCode, "The work source's response could not be read: unexpected token"),
            (error.Code, error.Message)
        );
    }

    [Fact]
    public void ForeignItem_WhenCreated_NamesTheItemAndTheSource()
    {
        // Arrange
        WorkItemId id = new("jira:PROJECT", "12");
        Error error;

        // Act
        error = WorkErrors.ForeignItem(id, "github:owner/repository");

        // Assert
        Assert.Equal(
            (WorkErrors.ForeignItemCode, "Work item jira:PROJECT#12 does not belong to github:owner/repository."),
            (error.Code, error.Message)
        );
    }
}