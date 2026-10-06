namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// The item is still waiting on a maintainer's answer to an earlier question,
/// so nothing was run: the specifier would only ask again.
/// </summary>
public sealed record StillWaiting : SpecifyOutcome
{
    public override TResult Match<TResult>(
        Func<Specified, TResult> specified,
        Func<AwaitingAnswers, TResult> awaitingAnswers,
        Func<StillWaiting, TResult> stillWaiting
    ) =>
        stillWaiting(this);
}