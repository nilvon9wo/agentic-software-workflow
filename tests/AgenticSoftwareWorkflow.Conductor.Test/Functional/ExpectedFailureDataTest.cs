using AgenticSoftwareWorkflow.Cli;
using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Conductor.Test.Functional;

/// <summary>
/// Each failure carries the data that explains it: the contract a failure's
/// handler (a repair loop deciding whether to retry, say) can rely on.
/// </summary>
public sealed class ExpectedFailureDataTest
{
    private static readonly WorkItemId Twelve = new("jira:PROJECT", "12");

    [Fact]
    public void AgentTimedOut_WhenCreated_CarriesItsTimeout()
    {
        // Arrange
        AgentTimedOut failure = new(TimeSpan.FromSeconds(30));

        // Act
        TimeSpan timeout = failure.Timeout;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), timeout);
    }

    [Fact]
    public void AgentProcessFailed_WhenCreated_CarriesItsExitCodeAndError()
    {
        // Arrange
        AgentProcessFailed failure = new(2, "not logged in");

        // Act
        (int ExitCode, string StandardError) data = (failure.ExitCode, failure.StandardError);

        // Assert
        Assert.Equal((2, "not logged in"), data);
    }

    [Fact]
    public void AgentOutputMalformed_WhenCreated_CarriesItsDetail()
    {
        // Arrange
        AgentOutputMalformed failure = new("unexpected token");

        // Act
        string detail = failure.Detail;

        // Assert
        Assert.Equal("unexpected token", detail);
    }

    [Fact]
    public void AgentReportedError_WhenCreated_CarriesItsKindAndDetail()
    {
        // Arrange
        AgentReportedError failure = new("error_max_turns", "ran out of turns");

        // Act
        (string Kind, string Detail) data = (failure.Kind, failure.Detail);

        // Assert
        Assert.Equal(("error_max_turns", "ran out of turns"), data);
    }

    [Fact]
    public void CommandFailed_WhenCreated_CarriesItsCommandExitCodeAndError()
    {
        // Arrange
        CommandFailed failure = new("gh issue list", 4, "not authenticated");

        // Act
        (string Command, int ExitCode, string StandardError) data = (
            failure.Command,
            failure.ExitCode,
            failure.StandardError
        );

        // Assert
        Assert.Equal(("gh issue list", 4, "not authenticated"), data);
    }

    [Fact]
    public void WorkResponseMalformed_WhenCreated_CarriesItsDetail()
    {
        // Arrange
        WorkResponseMalformed failure = new("unexpected token");

        // Act
        string detail = failure.Detail;

        // Assert
        Assert.Equal("unexpected token", detail);
    }

    [Fact]
    public void ForeignWorkItem_WhenCreated_CarriesTheItemAndTheSource()
    {
        // Arrange
        ForeignWorkItem failure = new(Twelve, "github:owner/repository");

        // Act
        (WorkItemId Id, string Source) data = (failure.Id, failure.Source);

        // Assert
        Assert.Equal((Twelve, "github:owner/repository"), data);
    }

    [Fact]
    public void SpecifierAnswerUnusable_WhenCreated_CarriesItsDetail()
    {
        // Arrange
        SpecifierAnswerUnusable failure = new("no output");

        // Act
        string detail = failure.Detail;

        // Assert
        Assert.Equal("no output", detail);
    }

    [Fact]
    public void SettingsUnreadable_WhenCreated_CarriesThePathAndReason()
    {
        // Arrange
        SettingsUnreadable failure = new("/repository/aswf.json", "missing");

        // Act
        (string Path, string Reason) data = (failure.Path, failure.Reason);

        // Assert
        Assert.Equal(("/repository/aswf.json", "missing"), data);
    }
}