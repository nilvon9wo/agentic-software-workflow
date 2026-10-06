namespace AgenticWorkflow.Workflows;

public sealed record StepExecution(string StepName, StepResult Result);

public enum WorkflowStatus
{
    Succeeded,
    Failed,
    Waiting
}

public sealed record WorkflowResult(
    WorkflowStatus Status,
    IReadOnlyList<StepExecution> Steps)
{
    public bool Succeeded
        => this.Status == WorkflowStatus.Succeeded;

    public bool Failed
        => this.Status == WorkflowStatus.Failed;

    public bool Waiting
        => this.Status == WorkflowStatus.Waiting;
}