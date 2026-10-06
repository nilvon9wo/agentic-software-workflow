using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Agents;

/// <summary>
/// The port through which the conductor gets AI work done. Claude Code is one
/// adapter; any provider able to honour a role's tool restrictions can be another.
/// </summary>
/// <remarks>
/// Expected failures — a timeout, a crash, an unreadable answer, an error the
/// agent reports — come back as a failed <see cref="Fin{A}"/>, so the workflow
/// decides what happens next. Exceptions are reserved for misconfiguration.
/// </remarks>
public interface IAgentRunner
{
    Task<Fin<AgentResult>> RunAsync(AgentTask task, CancellationToken cancellationToken);
}