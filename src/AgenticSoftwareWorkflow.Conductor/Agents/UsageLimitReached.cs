using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// The agent refused to run because a usage limit (a subscription's five-hour
/// or weekly allowance, say) is used up. Nothing is wrong with the work: it can
/// run again once the limit resets.
/// </summary>
/// <param name="Limit">Which limit, in the vendor's own words.</param>
/// <param name="ResetsAt">When it resets, if the vendor said.</param>
[PublicAPI] // the failure's data is its contract with whoever handles it
public sealed record UsageLimitReached(string Limit, Option<DateTimeOffset> ResetsAt)
    : ExpectedFailure($"The agent's usage limit ({Limit}) is reached; {DescribeReset(ResetsAt)}.")
{
    private static string DescribeReset(Option<DateTimeOffset> resetsAt) =>
        resetsAt.Match(
            Some: reset => $"it resets at {reset:u}",
            None: () => "no reset time was given"
        );
}