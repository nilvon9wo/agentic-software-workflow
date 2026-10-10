using AgenticSoftwareWorkflow.Conductor.Formatting;
using LanguageExt;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Formatting;

public sealed class NoFormatterTest
{
    [Fact]
    public async Task Format_WhenCalled_SucceedsWithoutDoingAnything()
    {
        // Arrange
        IFormatting formatter = new NoFormatter();

        // Act
        Fin<Unit> formatted = await formatter.Format("/repository", TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(formatted);
    }
}