namespace AgenticWorkflow.Workflows;

public sealed class WorkflowExecutionState
{
    public string WorkflowName { get; set; } = string.Empty;

    public WorkflowStatus Status { get; set; }

    public Dictionary<string, StepState> Steps { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> RetryCounts { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string? WaitingStep { get; set; }

    public string? WaitingReason { get; set; }

    public string? ResumeFromStep { get; set; }
}

public sealed class StepState
{
    public StepStatus Status { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }
}