using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Work;
using AgenticSoftwareWorkflow.Conductor.Workflow.Specify;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>
/// Builds one work item end to end: in a fresh workspace from the base branch,
/// runs the build stage against its approved specification, then commits what
/// the workers changed and proposes it. The workspace is always removed.
/// </summary>
/// <remarks>
/// An item is buildable once its specification is on the base branch — its
/// specification proposal merged — and it is labelled specified. A proposed
/// build marks it built, so it is never built twice.
/// </remarks>
public sealed class BuildCommand(
    BuildStage stage,
    IWorkSupplying work,
    GitRepository git,
    IChangeProposing changes,
    string baseBranch
)
{
    private const string BranchPrefix = "aswf/build-";

    private readonly BuildStage _stage = stage;
    private readonly IWorkSupplying _work = work;
    private readonly GitRepository _git = git;
    private readonly IChangeProposing _changes = changes;
    private readonly string _baseBranch = baseBranch;

    /// <summary>
    /// The specified items whose specification has merged into the base branch
    /// and is not being revised: an open specification proposal means the
    /// specification on the base branch is about to change, so it must not be
    /// built from.
    /// </summary>
    public async Task<Fin<IReadOnlyList<WorkItemId>>> ListBuildable(CancellationToken cancellationToken)
    {
        Fin<IReadOnlyList<WorkItemId>> specified = await this._work.ListSpecified(cancellationToken);
        Fin<IReadOnlyList<WorkItemId>> revising = await specified.Then(
            _ => this._changes.ListOpenSpecifications(cancellationToken)
        );
        Fin<IReadOnlyList<WorkItemId>> settled = specified.Bind(
            ids => revising.Map(IReadOnlyList<WorkItemId> (open) => [.. ids.Except(open)])
        );
        Fin<IReadOnlyList<string>> merged = await settled.Then(
            _ => this._git.FilesOnRemote(this._baseBranch, WorkspaceLayout.SpecificationDirectory, cancellationToken)
        );
        return await settled.Then(
            ids => merged.Then(files => this.WithMergedSpecification(ids, files, cancellationToken))
        );
    }

    public async Task<Fin<string>> Build(WorkItemId id, CancellationToken cancellationToken)
    {
        string branch = BranchPrefix + id.SafeKey;
        Fin<Workspace> workspace = await this._git.CreateWorkspace(branch, this._baseBranch, cancellationToken);
        Fin<string> report = await workspace.Then(created => this.BuildIn(id, created, cancellationToken));
        Fin<Unit> removed = await workspace.Then(created => this._git.RemoveWorkspace(created, cancellationToken));
        return report.Bind(message => removed.Map(_ => message));
    }

    private static string RelativeSpecificationPath(WorkItem item) =>
        $"{WorkspaceLayout.SpecificationDirectory}/{SpecificationFileName.For(item)}";

    private static Fin<string> ReadSpecification(WorkItem item, Workspace workspace)
    {
        string relativePath = RelativeSpecificationPath(item);
        string fullPath = Path.Combine(workspace.Path, relativePath);
        return File.Exists(fullPath)
            ? Fin.Succ(File.ReadAllText(fullPath))
            : Fin.Fail<string>(new SpecificationNotFound(item.Id, relativePath));
    }

    private static string DescribeProposal(BuildJob job, Built built) =>
        $"""
        Implements the specification for {job.Item.Id}: `{RelativeSpecificationPath(job.Item)}`.

        **Tests** — written by the test author, approved by the test reviewer:

        {built.TestsSummary}

        **Implementation** — written by the implementer; passed the project's
        checks and the code reviewer:

        {built.ImplementationSummary}

        This pull request does not merge by itself: a maintainer merges it.
        """;

    private async Task<Fin<IReadOnlyList<WorkItemId>>> WithMergedSpecification(
        IReadOnlyList<WorkItemId> ids,
        IReadOnlyList<string> mergedFiles,
        CancellationToken cancellationToken
    )
    {
        List<WorkItemId> buildable = [];
        foreach (WorkItemId id in ids)
        {
            Fin<WorkItem> item = await this._work.Read(id, cancellationToken);
            buildable.AddRange(item.Match(Succ: read => IdIfMerged(read, mergedFiles), Fail: _ => []));
        }

        return Fin.Succ<IReadOnlyList<WorkItemId>>(buildable);
    }

    private static IEnumerable<WorkItemId> IdIfMerged(WorkItem item, IReadOnlyList<string> mergedFiles) =>
        mergedFiles.Contains(RelativeSpecificationPath(item)) ? [item.Id] : [];

    private async Task<Fin<string>> BuildIn(WorkItemId id, Workspace workspace, CancellationToken cancellationToken)
    {
        Fin<WorkItem> item = await this._work.Read(id, cancellationToken);
        Fin<BuildJob> job = item.Bind(
            read => ReadSpecification(read, workspace)
                .Map(specification => new BuildJob(read, specification, workspace))
        );
        return await job.Then(planned => this.BuildAndPropose(planned, cancellationToken));
    }

    private async Task<Fin<string>> BuildAndPropose(BuildJob job, CancellationToken cancellationToken)
    {
        Fin<Built> built = await this._stage.Run(job, cancellationToken);
        return await built.Then(done => this.Propose(job, done, cancellationToken));
    }

    private async Task<Fin<string>> Propose(BuildJob job, Built built, CancellationToken cancellationToken)
    {
        WorkItemId id = job.Item.Id;
        string title = ProposalTitle.For("Build", id, job.Specification);
        Fin<IReadOnlyList<string>> changed = await this._git.ChangedPaths(job.Workspace, cancellationToken);
        Fin<Unit> committed = await changed.Then(
            paths => this._git.Commit(job.Workspace, paths, title, cancellationToken)
        );
        Fin<Unit> pushed = await committed.Then(_ => this._git.Push(job.Workspace, cancellationToken));
        ChangeProposal proposal = new(
            id,
            ProposalKind.Implementation,
            job.Workspace.Branch,
            title,
            DescribeProposal(job, built)
        );
        Fin<string> proposed = await pushed.Then(_ => this._changes.Propose(proposal, cancellationToken));
        Fin<Unit> marked = await proposed.Then(_ => this._work.MarkBuilt(id, cancellationToken));
        return proposed.Bind(address => marked.Map(_ => $"Proposed the build of {id} for review: {address}"));
    }
}