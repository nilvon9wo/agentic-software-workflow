namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>The specification was written, at a path relative to the working copy.</summary>
public sealed record Specified(string SpecificationPath) : SpecifyOutcome
{
    public override TResult Match<TResult>(
        Func<Specified, TResult> specified,
        Func<AwaitingAnswers, TResult> awaitingAnswers
    ) =>
        specified(this);
}