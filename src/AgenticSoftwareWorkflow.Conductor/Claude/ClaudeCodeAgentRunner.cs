using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Processes;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Claude;

/// <summary>
/// The Claude Code adapter for <see cref="IAgentic"/>: runs each task as a
/// headless `claude -p` process, so the work draws on a Claude subscription
/// rather than a pay-per-token API.
/// </summary>
public sealed class ClaudeCodeAgentRunner(IProcessCapable processRunner, string executable = "claude")
    : IAgentic
{
    private readonly IProcessCapable _processRunner = processRunner;
    private readonly string _executable = executable;

    public async Task<Fin<AgentResult>> Run(AgentTask task, CancellationToken cancellationToken)
    {
        ClaudeInvocation invocation = ClaudeTaskTranslator.ToInvocation(task);
        ProcessRequest request = new(
            this._executable,
            invocation.Arguments,
            task.Prompt,
            task.WorkingDirectory,
            task.Timeout
        );
        ProcessOutcome outcome = await this._processRunner.Run(request, cancellationToken);
        return ClaudeOutputReader.Read(outcome, task.Timeout);
    }
}