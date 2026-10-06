namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// A capability a role may be granted, independent of any vendor's tool names.
/// A role is granted exactly the capabilities its job needs: a reviewer reads
/// and searches, but never edits.
/// </summary>
public enum AgentTool
{
    /// <summary>Read file contents.</summary>
    ReadFiles,

    /// <summary>Find files and search their contents.</summary>
    SearchFiles,

    /// <summary>Create and change files in the working directory.</summary>
    EditFiles,

    /// <summary>Run shell commands, subject to the role's permission rules.</summary>
    RunCommands,
}