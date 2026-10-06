namespace AgenticWorkflow.Evaluation;

public sealed record CodeEvaluation(
    string Verdict,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Strengths,
    string Summary);
