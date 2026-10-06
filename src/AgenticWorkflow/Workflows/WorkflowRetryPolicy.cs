namespace AgenticWorkflow.Workflows;

public sealed record WorkflowRetryPolicy(
    IReadOnlyDictionary<string, string> RepairSteps,
    int MaximumAttempts)
{
    public bool TryGetRepairStep(
        string failedStep,
        out string? repairStep) => this.RepairSteps.TryGetValue(
            failedStep,
            out repairStep);
}