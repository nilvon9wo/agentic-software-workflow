namespace AgenticSoftwareWorkflow.Conductor.Workflow;

/// <summary>How the run loop paces itself.</summary>
/// <param name="RunOnce">Make one pass and stop, rather than looping until the process is stopped.</param>
/// <param name="PassInterval">How long to wait between passes.</param>
/// <param name="ResetMargin">How long after a usage limit's reset to retry, in case clocks differ.</param>
/// <param name="UnknownResetWait">How long to wait out a usage limit that gave no reset time.</param>
public sealed record RunLoopOptions(
    bool RunOnce,
    TimeSpan PassInterval,
    TimeSpan ResetMargin,
    TimeSpan UnknownResetWait
)
{
    /// <summary>A pass every ten minutes; a minute's grace after a reset; an hour when none is known.</summary>
    public static RunLoopOptions Continuous { get; } = new(
        false,
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromHours(1)
    );

    /// <summary>The same pacing, for a single pass.</summary>
    public static RunLoopOptions Once { get; } = Continuous with { RunOnce = true };
}