namespace AgenticWorkflow.Ai;

public sealed record AiRequest(
    string SystemPrompt,
    string UserPrompt,
    int MaxTokens);
