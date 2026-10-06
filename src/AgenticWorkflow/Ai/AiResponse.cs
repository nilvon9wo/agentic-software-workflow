namespace AgenticWorkflow.Ai;

public sealed record AiResponse(
    string Text,
    string Model,
    int InputTokens,
    int OutputTokens);
