namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// How the specify stage ended: see <see cref="Specified"/>,
/// <see cref="AwaitingAnswers"/>, and <see cref="StillWaiting"/>.
/// </summary>
public abstract record SpecifyOutcome
{
    /// <summary>
    /// Applies the function for whichever outcome this is. Callers handle every
    /// outcome without a type switch, and the compiler checks that they do.
    /// </summary>
    public abstract TResult Match<TResult>(
        Func<Specified, TResult> specified,
        Func<AwaitingAnswers, TResult> awaitingAnswers,
        Func<StillWaiting, TResult> stillWaiting
    );
}