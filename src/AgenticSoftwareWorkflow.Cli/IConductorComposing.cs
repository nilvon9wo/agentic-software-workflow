using AgenticSoftwareWorkflow.Conductor.Workflow;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

namespace AgenticSoftwareWorkflow.Cli;

/// <summary>
/// Builds what each <c>aswf</c> command runs, from the repository's settings.
/// <see cref="Composition"/> wires the real adapters; tests wire fakes.
/// </summary>
internal interface IConductorComposing
{
    SpecifyCommand CreateSpecifyCommand(ConductorSettings settings, string repositoryRoot);

    RunLoop CreateRunLoop(ConductorSettings settings, CommandContext context, RunLoopOptions options);
}