using AgenticSoftwareWorkflow.Conductor.Work;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// Names a specification's proposal (and its commit) after what it intends,
/// from the specification's own heading, with the work item's key as a
/// reference: <c>Specify: Parameterise tests that differ only by data (#25)</c>.
/// </summary>
internal static class ProposalTitle
{
    private const string HeadingStart = "# ";
    private const int MaximumHeadingLength = 100;

    public static string For(WorkItemId id, string specification)
    {
        string heading = specification
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith(HeadingStart, StringComparison.Ordinal), string.Empty);
        string intent = heading.Length == 0
            ? $"the work item {id}"
            : Shorten(heading[HeadingStart.Length..].Trim());
        return $"Specify: {intent} (#{id.Key})";
    }

    private static string Shorten(string heading) =>
        heading.Length <= MaximumHeadingLength
            ? heading
            : heading[..MaximumHeadingLength].TrimEnd() + "…";
}