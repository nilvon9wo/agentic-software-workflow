using System.Text;
using AgenticSoftwareWorkflow.Conductor.Work;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>
/// A build worker's brief: the work item as usual, then its approved
/// specification, then whatever this worker must act on — a reviewer's
/// findings, the checks' report, or the change to review.
/// </summary>
internal static class BuildBrief
{
    // A worker's brief is the same whichever OS the conductor runs on.
    private const char LineBreak = '\n';

    public static string Describe(BuildJob job, string heading, string content)
    {
        StringBuilder brief = new(WorkItemBrief.Describe(job.Item));
        AppendFenced(brief, "The approved specification", "markdown", job.Specification);
        if (content.Length > 0)
        {
            _ = brief.Append(LineBreak);
            _ = brief.Append($"## {heading}").Append(LineBreak);
            _ = brief.Append(LineBreak);
            _ = brief.Append(content.TrimEnd()).Append(LineBreak);
        }

        return brief.ToString();
    }

    /// <summary>The brief for a reviewer, with the change it judges shown as a diff.</summary>
    public static string ForReview(BuildJob job, string diff)
    {
        StringBuilder brief = new(Describe(job, string.Empty, string.Empty));
        AppendFenced(brief, "The change to review", "diff", diff);
        return brief.ToString();
    }

    // Four backticks, so a fenced block inside the content cannot end the fence.
    private static void AppendFenced(StringBuilder brief, string heading, string language, string content)
    {
        _ = brief.Append(LineBreak);
        _ = brief.Append($"## {heading}").Append(LineBreak);
        _ = brief.Append(LineBreak);
        _ = brief.Append($"````{language}").Append(LineBreak);
        _ = brief.Append(content.TrimEnd()).Append(LineBreak);
        _ = brief.Append("````").Append(LineBreak);
    }
}