using AgenticWorkflow.Ai;
using Xunit;

namespace AgenticWorkflow.Tests;

public sealed class ClaudeCodeAiProviderTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsClaudeResponse()
    {
        ClaudeCodeAiProvider provider = new();

        AiRequest request = new(
            "You are a test assistant. Follow the user's instruction exactly.",
            "Return exactly the word AUTHENTICATED and nothing else.",
            100);

        AiResponse response = await provider.CompleteAsync(request);

        Assert.Equal("AUTHENTICATED", response.Text.Trim());
        Assert.NotEqual("unknown", response.Model);
        Assert.True(response.OutputTokens > 0);
    }
}
