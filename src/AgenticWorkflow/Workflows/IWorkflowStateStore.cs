namespace AgenticWorkflow.Workflows;

public interface IWorkflowStateStore
{
    Task<WorkflowExecutionState?> LoadAsync(string workflowName, CancellationToken cancellationToken);

    Task SaveAsync(WorkflowExecutionState state, CancellationToken cancellationToken);
}