using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Functional;

/// <summary>
/// Chains asynchronous steps that each may fail, so a workflow reads as a
/// sequence of steps rather than a ladder of success checks: each step runs
/// only if the previous one succeeded, and the first failure is the result.
/// </summary>
public static class FinSteps
{
    public static Task<Fin<TNext>> Then<TPrevious, TNext>(
        this Fin<TPrevious> previous,
        Func<TPrevious, Task<Fin<TNext>>> next
    ) =>
        previous.Match(
            Succ: next,
            Fail: error => Task.FromResult(Fin.Fail<TNext>(error))
        );
}