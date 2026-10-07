using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Build;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Workflow;

/// <summary>
/// Every stage the run loop drives, behind one port: specifying (and revising
/// specifications), and — when the project names the checks code must pass —
/// building approved specifications.
/// </summary>
public sealed class Pipeline(SpecifyCommand specify, Option<BuildCommand> build) : IWorkProcessing
{
    private readonly SpecifyCommand _specify = specify;
    private readonly Option<BuildCommand> _build = build;

    public Task<Fin<string>> Run(WorkItemId id, CancellationToken cancellationToken) =>
        this._specify.Run(id, cancellationToken);

    public Task<Fin<IReadOnlyList<string>>> UpdateBehindProposals(CancellationToken cancellationToken) =>
        this._specify.UpdateBehindProposals(cancellationToken);

    public Task<Fin<IReadOnlyList<WorkItemId>>> ListAwaitingRevision(CancellationToken cancellationToken) =>
        this._specify.ListAwaitingRevision(cancellationToken);

    public Task<Fin<string>> Revise(WorkItemId id, CancellationToken cancellationToken) =>
        this._specify.Revise(id, cancellationToken);

    public Task<Fin<IReadOnlyList<WorkItemId>>> ListBuildable(CancellationToken cancellationToken) =>
        this._build.Match(
            Some: builder => builder.ListBuildable(cancellationToken),
            None: () => Task.FromResult(Fin.Succ<IReadOnlyList<WorkItemId>>([]))
        );

    public Task<Fin<string>> Build(WorkItemId id, CancellationToken cancellationToken) =>
        this._build.Match(
            Some: builder => builder.Build(id, cancellationToken),
            None: () => Task.FromResult(Fin.Fail<string>(new BuildingNotConfigured()))
        );
}