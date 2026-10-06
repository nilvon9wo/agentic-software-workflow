namespace AgenticWorkflow.Workflows;

public enum StepStatus
{
    Succeeded,
    Failed,
    Waiting,
    Retry
}

public sealed record StepResult(
    StepStatus Status,
    string Message,
    string? ResumeFromStep = null)
{
    public bool Succeeded
        => this.Status == StepStatus.Succeeded;

    public bool Failed
        => this.Status == StepStatus.Failed;

    public bool Waiting
        => this.Status == StepStatus.Waiting;

    public bool Retry
        => this.Status == StepStatus.Retry;

    public static StepResult Success(string message)
        => new(
            StepStatus.Succeeded,
            message);

    public static StepResult Failure(string message)
        => new(
            StepStatus.Failed,
            message);

    public static StepResult Wait(
        string message,
        string? resumeFromStep = null)
        => new(
            StepStatus.Waiting,
            message,
            resumeFromStep);

    public static StepResult RetryFrom(
        string message,
        string resumeFromStep)
        => new(
            StepStatus.Retry,
            message,
            resumeFromStep);
}