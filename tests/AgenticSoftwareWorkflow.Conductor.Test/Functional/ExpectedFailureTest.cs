using AgenticSoftwareWorkflow.Cli;
using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Conductor.Test.Functional;

/// <summary>
/// Every expected failure explains itself in a sentence a person can act on.
/// Which failure occurred is asserted elsewhere, by type and value; this pins
/// what each one says.
/// </summary>
public sealed class ExpectedFailureTest
{
    private static readonly Dictionary<string, ExpectedFailure> FailuresByName = new()
    {
        [nameof(AgentTimedOut)] = new AgentTimedOut(TimeSpan.FromSeconds(30)),
        [nameof(AgentProcessFailed)] = new AgentProcessFailed(2, "  not logged in\n"),
        [nameof(AgentOutputMalformed)] = new AgentOutputMalformed("unexpected token"),
        [nameof(AgentReportedError)] = new AgentReportedError("error_max_turns", "ran out of turns"),
        [nameof(CommandFailed)] = new CommandFailed("gh issue list", 4, " not authenticated\n"),
        [nameof(WorkResponseMalformed)] = new WorkResponseMalformed("unexpected token"),
        [nameof(ForeignWorkItem)] = new ForeignWorkItem(
            new WorkItemId("jira:PROJECT", "12"),
            "github:owner/repository"
        ),
        [nameof(SpecifierAnswerUnusable)] = new SpecifierAnswerUnusable("no output"),
        [nameof(SettingsUnreadable)] = new SettingsUnreadable("/repository/aswf.json", "missing"),
    };

    [Theory]
    [InlineData(nameof(AgentTimedOut), "The agent did not finish within 00:00:30.")]
    [InlineData(nameof(AgentProcessFailed), "The agent process exited with code 2: not logged in")]
    [InlineData(nameof(AgentOutputMalformed), "The agent's output could not be read: unexpected token")]
    [InlineData(nameof(AgentReportedError), "The agent reported an error (error_max_turns): ran out of turns")]
    [InlineData(nameof(CommandFailed), "'gh issue list' exited with code 4: not authenticated")]
    [InlineData(nameof(WorkResponseMalformed), "The work source's response could not be read: unexpected token")]
    [InlineData(nameof(ForeignWorkItem), "Work item jira:PROJECT#12 does not belong to github:owner/repository.")]
    [InlineData(nameof(SpecifierAnswerUnusable), "The specifier's answer cannot be used: no output")]
    [InlineData(
        nameof(SettingsUnreadable),
        "The conductor's settings at /repository/aswf.json cannot be read: missing"
    )]
    public void Message_WhenAFailureOccurs_ExplainsItInASentence(string failureName, string expectedMessage)
    {
        // Arrange
        ExpectedFailure failure = FailuresByName[failureName];
        string message;

        // Act
        message = failure.Message;

        // Assert
        Assert.Equal(expectedMessage, message);
    }

    [Fact]
    public void IsExpected_WhenAFailureOccurs_IsTrue()
    {
        // Arrange
        ExpectedFailure failure = new AgentTimedOut(TimeSpan.FromSeconds(1));
        bool isExpected;

        // Act
        isExpected = failure.IsExpected;

        // Assert
        Assert.True(isExpected);
    }
}