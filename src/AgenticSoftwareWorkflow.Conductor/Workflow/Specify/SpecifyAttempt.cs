using AgenticSoftwareWorkflow.Conductor.Work;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// One run of the specifier: the item, the workspace it writes into, the brief
/// it is given, and whether a failed check may still be repaired.
/// </summary>
/// <param name="MayRepair">
/// True on the first attempt only: a specification that fails the project's
/// checks gets one repair, the vision's repair loop at its smallest.
/// </param>
internal sealed record SpecifyAttempt(WorkItem Item, string WorkingDirectory, string Brief, bool MayRepair)
{
    public static SpecifyAttempt First(WorkItem item, string workingDirectory) =>
        new(item, workingDirectory, WorkItemBrief.Describe(item), true);

    /// <summary>The last attempt: the same brief, plus what failed and what was written.</summary>
    public SpecifyAttempt Repairing(string specification, string report) =>
        this with
        {
            Brief = $"""
                {this.Brief}

                ## Your specification failed the project's checks

                {report}

                This is what you wrote:

                ````markdown
                {specification}
                ````

                Fix every reported problem, change nothing else, and answer again.
                """,
            MayRepair = false,
        };
}