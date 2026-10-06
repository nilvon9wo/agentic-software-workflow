namespace AgenticWorkflow.Workflows;

public interface IWorkflowStep
{
    string Name { get; }

    Task<StepResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken);
}
