using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Gates;

/// <summary>
/// The port through which the conductor holds what workers write to the
/// project's own deterministic checks, before proposing it to anyone.
/// </summary>
public interface IGateKeeping
{
    /// <summary>
    /// Passes when the working copy at <paramref name="workingDirectory"/> passes
    /// the checks; otherwise fails with <see cref="GatesFailed"/> and their report.
    /// </summary>
    Task<Fin<Unit>> Check(string workingDirectory, CancellationToken cancellationToken);
}