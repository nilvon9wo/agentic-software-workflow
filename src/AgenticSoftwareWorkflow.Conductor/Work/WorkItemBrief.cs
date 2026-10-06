using System.Text;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// How a work item is presented to a worker: what was asked for, and what the
/// maintainers have said since. Untrusted comments are left out entirely —
/// on a public repository anyone can comment, and a worker must never read a
/// stranger's text as instructions.
/// </summary>
public static class WorkItemBrief
{
    // A worker's brief is the same whichever OS the conductor runs on.
    private const char LineBreak = '\n';

    public static string Describe(WorkItem item)
    {
        StringBuilder brief = new();
        _ = brief.Append($"# {item.Title}").Append(LineBreak);
        _ = brief.Append(LineBreak);
        _ = brief.Append(item.Body).Append(LineBreak);
        foreach (WorkComment comment in item.TrustedComments)
        {
            _ = brief.Append(LineBreak);
            _ = brief.Append($"## {comment.Author} replied").Append(LineBreak);
            _ = brief.Append(LineBreak);
            _ = brief.Append(comment.Body).Append(LineBreak);
        }

        return brief.ToString();
    }
}