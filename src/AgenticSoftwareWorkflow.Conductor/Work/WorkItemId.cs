using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Work;

/// <summary>
/// Identifies one piece of work across every source: which source it came from
/// (<c>github:owner/repository</c>, say) and its key within that source.
/// </summary>
public sealed record WorkItemId(string Source, string Key)
{
    private const char Replacement = '-';
    private const char KeySeparator = '#';

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

    /// <summary>The id <see cref="ToString"/> wrote, or none if the text is not one.</summary>
    public static Option<WorkItemId> Parse(string text)
    {
        int separator = text.LastIndexOf(KeySeparator);
        return separator > 0 && separator < text.Length - 1
            ? new WorkItemId(text[..separator], text[(separator + 1)..])
            : Option<WorkItemId>.None;
    }

    public override string ToString() => $"{this.Source}{KeySeparator}{this.Key}";
}