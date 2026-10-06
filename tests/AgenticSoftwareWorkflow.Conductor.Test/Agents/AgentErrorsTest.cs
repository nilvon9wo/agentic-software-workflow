using AgenticSoftwareWorkflow.Conductor.Agents;
using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Test.Agents;

public sealed class AgentErrorsTest
{
    [Fact]
    public void TimedOut_WhenCreated_CarriesItsCodeAndTheTimeout()
    {
        // Arrange
        Error error;

        // Act
        error = AgentErrors.TimedOut(TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(
            (AgentErrors.TimedOutCode, "The agent did not finish within 00:00:30."),
            (error.Code, error.Message)
        );
    }

    [Fact]
    public void ProcessFailed_WhenCreated_CarriesItsCodeExitCodeAndTrimmedStandardError()
    {
        // Arrange
        Error error;

        // Act
        error = AgentErrors.ProcessFailed(2, "  not logged in\n");

        // Assert
        Assert.Equal(
            (AgentErrors.ProcessFailedCode, "The agent process exited with code 2: not logged in"),
            (error.Code, error.Message)
        );
    }

    [Fact]
    public void MalformedOutput_WhenCreated_CarriesItsCodeAndDetail()
    {
        // Arrange
        Error error;

        // Act
        error = AgentErrors.MalformedOutput("unexpected token");

        // Assert
        Assert.Equal(
            (AgentErrors.MalformedOutputCode, "The agent's output could not be read: unexpected token"),
            (error.Code, error.Message)
        );
    }

    [Fact]
    public void AgentReportedError_WhenCreated_CarriesItsCodeKindAndMessage()
    {
        // Arrange
        Error error;

        // Act
        error = AgentErrors.AgentReportedError("error_max_turns", "ran out of turns");

        // Assert
        Assert.Equal(
            (AgentErrors.AgentReportedErrorCode, "The agent reported an error (error_max_turns): ran out of turns"),
            (error.Code, error.Message)
        );
    }
}