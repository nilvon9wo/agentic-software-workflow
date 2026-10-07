namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>An answer a worker gives as JSON constrained by a schema, so the stage reads a contract.</summary>
internal interface IStructuredAnswer
{
    /// <summary>The JSON schema the worker's structured output must satisfy.</summary>
    static abstract string Schema { get; }
}