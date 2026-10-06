using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Claude;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Claude;

public sealed class ClaudeOutputReaderTest
{
    private const int Success = 0;
    private const int Failure = 1;

    private const string CompleteEnvelope = """
        {
          "type": "result",
          "subtype": "success",
          "is_error": false,
          "result": "{\"answer\":\"ok\"}",
          "structured_output": { "answer": "ok" },
          "total_cost_usd": 0.0221,
          "num_turns": 3,
          "usage": { "input_tokens": 19, "output_tokens": 254 },
          "modelUsage": { "claude-haiku-4-5-20251001": {} },
          "permission_denials": []
        }
        """;

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    [Fact]
    public void Read_WhenTheProcessTimedOut_FailsWithTimedOut()
    {
        // Arrange
        ProcessOutcome outcome = new(-1, "", "", true);
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        _ = Assert.IsType<AgentTimedOut>(AssertFailure(result));
    }

    [Fact]
    public void Read_WhenTheEnvelopeIsComplete_ReturnsItsAnswerAndUsage()
    {
        // Arrange
        ProcessOutcome outcome = Finished(Success, CompleteEnvelope);
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        AgentResult answer = AssertSuccess(result);
        Assert.Equal(
            ("{\"answer\":\"ok\"}", 19, 254, 0.0221m, 3, "claude-haiku-4-5-20251001"),
            (
                answer.Text,
                answer.Usage.InputTokens,
                answer.Usage.OutputTokens,
                answer.Usage.ListPriceUsd,
                answer.Usage.Turns,
                Assert.Single(answer.Usage.Models)
            )
        );
    }

    [Fact]
    public void Read_WhenTheEnvelopeHasStructuredOutput_ReturnsItsJson()
    {
        // Arrange
        ProcessOutcome outcome = Finished(Success, CompleteEnvelope);
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        Assert.Equal(Prelude.Some("{ \"answer\": \"ok\" }"), AssertSuccess(result).StructuredOutputJson);
    }

    [Fact]
    public void Read_WhenTheEnvelopeHasOnlyTheEssentials_DefaultsTheRest()
    {
        // Arrange
        ProcessOutcome outcome = Finished(Success, """{ "is_error": false }""");
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        AgentResult answer = AssertSuccess(result);
        Assert.Equal(
            ("", Option<string>.None, 0, 0, 0m, 0, 0, 0),
            (
                answer.Text,
                answer.StructuredOutputJson,
                answer.Usage.InputTokens,
                answer.Usage.OutputTokens,
                answer.Usage.ListPriceUsd,
                answer.Usage.Turns,
                answer.Usage.Models.Count,
                answer.DeniedTools.Count
            )
        );
    }

    [Fact]
    public void Read_WhenToolsWereDenied_ReportsEachDenial()
    {
        // Arrange
        const string envelope = """
            {
              "is_error": false,
              "permission_denials": [ { "tool_name": "Bash", "tool_use_id": "1" }, { "unexpected": true } ]
            }
            """;
        ProcessOutcome outcome = Finished(Success, envelope);
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        Assert.Equal(["Bash", "{ \"unexpected\": true }"], AssertSuccess(result).DeniedTools);
    }

    [Fact]
    public void Read_WhenADenialNamesNoToolAsText_ReportsTheRawEntry()
    {
        // Arrange
        const string envelope = """{ "is_error": false, "permission_denials": [ { "tool_name": 7 }, "Edit" ] }""";
        ProcessOutcome outcome = Finished(Success, envelope);
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        Assert.Equal(["{ \"tool_name\": 7 }", "\"Edit\""], AssertSuccess(result).DeniedTools);
    }

    [Fact]
    public void Read_WhenTheAgentReportsAnError_FailsWithItsKindAndMessage()
    {
        // Arrange
        const string envelope = """{ "is_error": true, "subtype": "error_max_turns", "result": "out of turns" }""";
        ProcessOutcome outcome = Finished(Failure, envelope);
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        Assert.Equal(new AgentReportedError("error_max_turns", "out of turns"), AssertFailure(result));
    }

    [Fact]
    public void Read_WhenTheAgentReportsAnUnnamedError_CallsItUnknown()
    {
        // Arrange
        ProcessOutcome outcome = Finished(Success, """{ "is_error": true }""");
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        Assert.Equal("The agent reported an error (unknown): ", AssertFailure(result).Message);
    }

    [Fact]
    public void Read_WhenTheOutputIsNotJson_FailsWithMalformedOutput()
    {
        // Arrange
        ProcessOutcome outcome = Finished(Success, "Hello!");
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        _ = Assert.IsType<AgentOutputMalformed>(AssertFailure(result));
    }

    [Fact]
    public void Read_WhenTheOutputIsJsonNull_FailsWithMalformedOutput()
    {
        // Arrange
        ProcessOutcome outcome = Finished(Success, "null");
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        Assert.Equal(
            "The agent's output could not be read: the output was the JSON literal null",
            AssertFailure(result).Message
        );
    }

    [Fact]
    public void Read_WhenTheProcessFailedWithoutJson_FailsWithProcessFailed()
    {
        // Arrange
        ProcessOutcome outcome = new(Failure, "", "Not logged in", false);
        Fin<AgentResult> result;

        // Act
        result = ClaudeOutputReader.Read(outcome, Timeout);

        // Assert
        Assert.Equal(new AgentProcessFailed(1, "Not logged in"), AssertFailure(result));
    }

    private static ProcessOutcome Finished(int exitCode, string standardOutput) =>
        new(exitCode, standardOutput, "", false);
}