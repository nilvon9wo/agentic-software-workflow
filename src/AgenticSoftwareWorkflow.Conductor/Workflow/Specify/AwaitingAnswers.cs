namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// The item was not clear enough: these questions were posted to it, and it is
/// now waiting on a maintainer.
/// </summary>
public sealed record AwaitingAnswers(IReadOnlyList<string> Questions) : SpecifyOutcome
{
    public override TResult Match<TResult>(
        Func<Specified, TResult> specified,
        Func<AwaitingAnswers, TResult> awaitingAnswers,
        Func<StillWaiting, TResult> stillWaiting
    ) =>
        awaitingAnswers(this);
}