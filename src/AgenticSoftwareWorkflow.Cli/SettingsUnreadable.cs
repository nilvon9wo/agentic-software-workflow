using AgenticSoftwareWorkflow.Conductor.Functional;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>The conductor's settings file is missing or cannot be read.</summary>
internal sealed record SettingsUnreadable : ExpectedFailure
{
    public SettingsUnreadable(string path, string reason)
        : base($"The conductor's settings at {path} cannot be read: {reason}")
    {
    }
}