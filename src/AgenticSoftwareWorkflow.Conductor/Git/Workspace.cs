namespace AgenticSoftwareWorkflow.Conductor.Git;

/// <summary>
/// An isolated working copy for one piece of work: its own directory, on its
/// own branch, so concurrent work never shares files and a role can be given a
/// copy with only what it is allowed to see.
/// </summary>
public sealed record Workspace(string Path, string Branch);