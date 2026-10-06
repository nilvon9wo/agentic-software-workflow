using System.Collections.Frozen;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// The subset of Claude Code permission modes a headless workflow role may use.
/// <c>bypassPermissions</c> is deliberately absent: an unattended role that can
/// skip every permission check cannot be held to a boundary.
/// </summary>
public enum ClaudePermissionMode
{
    /// <summary>Read-only planning; no edits or commands.</summary>
    Plan,

    /// <summary>Edits inside the working directory are allowed without prompting.</summary>
    AcceptEdits,

    /// <summary>Anything not explicitly allowed by settings is denied rather than prompted for.</summary>
    DontAsk,
}

internal static class ClaudePermissionModes
{
    private static readonly FrozenDictionary<ClaudePermissionMode, string> Arguments =
        new Dictionary<ClaudePermissionMode, string>
        {
            [ClaudePermissionMode.Plan] = "plan",
            [ClaudePermissionMode.AcceptEdits] = "acceptEdits",
            [ClaudePermissionMode.DontAsk] = "dontAsk",
        }.ToFrozenDictionary();

    public static string ToArgument(ClaudePermissionMode permissionMode)
    {
        bool isKnownMode = Arguments.TryGetValue(permissionMode, out string? argument);
        return isKnownMode
            ? argument!
            : throw new ArgumentOutOfRangeException(nameof(permissionMode), permissionMode, null);
    }
}