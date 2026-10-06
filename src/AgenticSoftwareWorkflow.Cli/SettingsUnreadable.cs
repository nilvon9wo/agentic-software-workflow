using AgenticSoftwareWorkflow.Conductor.Functional;
using JetBrains.Annotations;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>The conductor's settings file is missing or cannot be read.</summary>
[PublicAPI] // the failure\'s data is its contract with whoever handles it
internal sealed record SettingsUnreadable(string Path, string Reason)
    : ExpectedFailure($"The conductor's settings at {Path} cannot be read: {Reason}");