namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// Identifies one piece of work across every source: which source it came from
/// (<c>github:owner/repository</c>, say) and its key within that source.
/// </summary>
public sealed record WorkItemId(string Source, string Key)
{
    private const char Replacement = '-';

    /// <summary>
    /// The key, safe to use in a file or branch name: anything a file name
    /// cannot hold, and any slash, becomes a hyphen.
    /// </summary>
    public string SafeKey
    {
        get
        {
            char[] invalid = [.. Path.GetInvalidFileNameChars(), '/', '\\'];
            char[] safe = [.. this.Key.Select(character => invalid.Contains(character) ? Replacement : character)];
            return new string(safe);
        }
    }

    public override string ToString() => $"{this.Source}#{this.Key}";
}