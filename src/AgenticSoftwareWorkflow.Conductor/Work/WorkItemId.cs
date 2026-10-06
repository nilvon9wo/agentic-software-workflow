namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// Identifies one piece of work across every source: which source it came from
/// (<c>github:owner/repository</c>, say) and its key within that source.
/// </summary>
public sealed record WorkItemId(string Source, string Key)
{
    public override string ToString() => $"{this.Source}#{this.Key}";
}