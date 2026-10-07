using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Workflow.Build;
using LanguageExt;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Workflow.Build;

public sealed class StructuredAnswersTest
{
    private static readonly AgentUsage NoUsage = new(0, 0, 0m, 0, []);

    [Fact]
    public void Read_WhenThereIsNoStructuredOutput_FailsSayingSo()
    {
        // Arrange
        AgentResult result = new("Done.", Option<string>.None, NoUsage, []);

        // Act
        Fin<WorkerReport> report = StructuredAnswers.Read<WorkerReport>(result);

        // Assert
        Assert.Equal(
            "A worker's answer cannot be used: there was no structured output",
            AssertFailure(report).Message
        );
    }

    [Fact]
    public void Read_WhenTheAnswerIsJsonNull_FailsSayingSo()
    {
        // Arrange
        AgentResult result = new("", Prelude.Some("null"), NoUsage, []);

        // Act
        Fin<WorkerReport> report = StructuredAnswers.Read<WorkerReport>(result);

        // Assert
        Assert.Equal(
            "A worker's answer cannot be used: the answer was the JSON literal null",
            AssertFailure(report).Message
        );
    }

    [Fact]
    public void Read_WhenTheAnswerIsNotJson_FailsWithUnusableAnswer()
    {
        // Arrange
        AgentResult result = new("", Prelude.Some("{ not json"), NoUsage, []);

        // Act
        Fin<WorkerReport> report = StructuredAnswers.Read<WorkerReport>(result);

        // Assert
        WorkerAnswerUnusable unusable = Assert.IsType<WorkerAnswerUnusable>(AssertFailure(report));
        Assert.NotEmpty(unusable.Detail);
    }

    [Fact]
    public void Read_WhenTheAnswerIsAVerdict_ReadsItsFindings()
    {
        // Arrange
        AgentResult result = new("", Prelude.Some("""{"verdict":"revise","findings":["One.","Two."]}"""), NoUsage, []);

        // Act
        Fin<ReviewVerdict> verdict = StructuredAnswers.Read<ReviewVerdict>(result);

        // Assert
        Assert.Equal("- One.\n- Two.", AssertSuccess(verdict).Describe());
    }
}