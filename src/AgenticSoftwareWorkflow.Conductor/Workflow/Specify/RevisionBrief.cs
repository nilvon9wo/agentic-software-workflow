using System.Text;
using AgenticSoftwareWorkflow.Conductor.Work;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// The specifier's brief for revising a specification under review: the work
/// item as usual, then the specification it proposed and what maintainers have
/// said about it since, oldest first.
/// </summary>
internal static class RevisionBrief
{
    // A worker's brief is the same whichever OS the conductor runs on.
    private const char LineBreak = '\n';

    public static string Describe(WorkItem item, string currentSpecification, IReadOnlyList<WorkComment> feedback)
    {
        StringBuilder brief = new(WorkItemBrief.Describe(item));
        _ = brief.Append(LineBreak);
        _ = brief.Append("## Your specification is under review").Append(LineBreak);
        _ = brief.Append(LineBreak);
        _ = brief.Append("This is the specification you proposed:").Append(LineBreak);
        _ = brief.Append(LineBreak);
        _ = brief.Append("````markdown").Append(LineBreak);
        _ = brief.Append(currentSpecification.TrimEnd()).Append(LineBreak);
        _ = brief.Append("````").Append(LineBreak);
        foreach (WorkComment comment in feedback)
        {
            _ = brief.Append(LineBreak);
            _ = brief.Append($"### {comment.Author} reviewed").Append(LineBreak);
            _ = brief.Append(LineBreak);
            _ = brief.Append(comment.Body).Append(LineBreak);
        }

        _ = brief.Append(LineBreak);
        _ = brief.Append(
            "Revise the specification so it answers every point of that review, and keep what was not "
            + "questioned. Record each change you made because of the review under Decisions. If a point "
            + "cannot be answered without guessing, ask instead."
        ).Append(LineBreak);
        return brief.ToString();
    }
}