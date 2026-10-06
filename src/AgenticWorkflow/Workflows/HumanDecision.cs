namespace AgenticWorkflow.Workflows;

public sealed record HumanDecision(
    string DecisionId,
    string Question,
    string Answer,
    string Rationale);