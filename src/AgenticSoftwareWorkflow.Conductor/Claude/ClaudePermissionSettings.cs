using System.Text.Json;
using AgenticSoftwareWorkflow.Conductor.Agents;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// Translates a role's <see cref="AgentAccess"/> into Claude Code permission
/// rules, passed inline with <c>--settings</c> so there is no settings file to
/// drift from the role that defines it.
/// </summary>
/// <remarks>
/// Deny rules win over everything, including a run's permission mode. Path
/// rules are prefixed with <c>./</c>, which Claude Code resolves against the
/// working directory. A command rule allows the command and any arguments.
/// </remarks>
internal static class ClaudePermissionSettings
{
    public static Option<string> For(AgentAccess access)
    {
        string[] allow = [.. access.AllowedCommands.Select(CommandRule)];
        string[] deny = [.. access.UnreadablePaths.Select(ReadRule), .. access.UneditablePaths.Select(EditRule)];
        bool hasRules = allow.Length > 0 || deny.Length > 0;
        return hasRules
            ? Serialize(allow, deny)
            : Option<string>.None;
    }

    private static string CommandRule(string command) => $"Bash({command}:*)";

    private static string ReadRule(string path) => $"Read(./{path})";

    private static string EditRule(string path) => $"Edit(./{path})";

    private static string Serialize(string[] allow, string[] deny)
    {
        var settings = new { permissions = new { allow, deny } };
        return JsonSerializer.Serialize(settings);
    }
}