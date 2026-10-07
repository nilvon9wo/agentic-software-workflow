using System.Reflection;

namespace AgenticSoftwareWorkflow.Conductor.Workflow;

/// <summary>
/// Each role's standing procedure, written as Markdown under
/// Workflow/Instructions and shipped inside the conductor, so the procedure is
/// versioned, reviewed, and linted like any other document.
/// </summary>
public static class RoleInstructions
{
    private const string ResourcePrefix = "AgenticSoftwareWorkflow.Conductor.Workflow.Instructions.";

    public static string Specifier { get; } = Load("specifier.md");

    public static string TestAuthor { get; } = Load("test-author.md");

    public static string TestReviewer { get; } = Load("test-reviewer.md");

    public static string Implementer { get; } = Load("implementer.md");

    public static string CodeReviewer { get; } = Load("code-reviewer.md");

    internal static string Load(string fileName)
    {
        Assembly assembly = typeof(RoleInstructions).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException(
                $"Role instructions '{fileName}' are not embedded; check the conductor's csproj."
            );
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}