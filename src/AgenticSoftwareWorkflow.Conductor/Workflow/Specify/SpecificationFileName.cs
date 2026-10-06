using System.Globalization;
using System.Text.RegularExpressions;
using AgenticSoftwareWorkflow.Conductor.Work;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// Names a work item's specification file so the name says what it is about:
/// <c>8-extend-the-gates-to-markdown-and-workflow-files-locally.md</c>. The
/// leading key keeps a stable link to the work item, so the file can still be
/// found when the item's title changes; the title says what it means.
/// </summary>
public static partial class SpecificationFileName
{
    private const int MaximumSlugLength = 60;
    private const char Separator = '-';
    private const string Extension = ".md";

    public static string For(WorkItem item)
    {
        string slug = Slug(item.Title);
        return slug.Length == 0
            ? item.Id.SafeKey + Extension
            : $"{item.Id.SafeKey}{Separator}{slug}{Extension}";
    }

    /// <summary>
    /// Lower case, each run of anything but letters and digits as one hyphen,
    /// cut at a word boundary so the name stays readable.
    /// </summary>
    private static string Slug(string title)
    {
        string lowered = title.ToLower(CultureInfo.InvariantCulture);
        string hyphenated = NonAlphanumeric().Replace(lowered, Separator.ToString());
        string trimmed = hyphenated.Trim(Separator);
        return Shorten(trimmed);
    }

    private static string Shorten(string slug)
    {
        if (slug.Length <= MaximumSlugLength)
        {
            return slug;
        }

        string prefix = slug[..(MaximumSlugLength + 1)];
        int lastBoundary = prefix.LastIndexOf(Separator);
        return lastBoundary > 0
            ? prefix[..lastBoundary]
            : slug[..MaximumSlugLength];
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}