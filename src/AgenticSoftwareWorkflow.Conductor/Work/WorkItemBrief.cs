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
    public static string Describe(WorkItem item)
    {
        StringBuilder brief = new();
        _ = brief.AppendLine($"# {item.Title}");
        _ = brief.AppendLine();
        _ = brief.AppendLine(item.Body);
        foreach (WorkComment comment in item.TrustedComments)
        {
            _ = brief.AppendLine();
            _ = brief.AppendLine($"## {comment.Author} replied");
            _ = brief.AppendLine();
            _ = brief.AppendLine(comment.Body);
        }

        return brief.ToString();
    }
}