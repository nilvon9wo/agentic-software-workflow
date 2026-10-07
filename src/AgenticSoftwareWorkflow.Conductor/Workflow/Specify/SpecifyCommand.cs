using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Git;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Specify;

/// <summary>
/// Specifies one work item end to end: in a fresh workspace, runs the specify
/// stage, then either proposes the specification for a maintainer's approval
/// or leaves the item waiting on their answers. The workspace is always
/// removed afterwards.
/// </summary>
public sealed class SpecifyCommand(
    SpecifyStage stage,
    IWorkSupplying work,
    GitRepository git,
    IChangeProposing changes,
    string baseBranch
) : IWorkProcessing
{
    private const string BranchPrefix = "aswf/specify-";
    private const string Revised =
        "Revised the specification in response to the review above; see the latest commit.";
    private const string QuestionsInstead =
        "The specifier needs answers before it can revise this; it has asked them on the issue.";

    private readonly SpecifyStage _stage = stage;
    private readonly IWorkSupplying _work = work;
    private readonly GitRepository _git = git;
    private readonly IChangeProposing _changes = changes;
    private readonly string _baseBranch = baseBranch;

    public async Task<Fin<string>> Run(WorkItemId id, CancellationToken cancellationToken)
    {
        string branch = BranchPrefix + id.SafeKey;
        Fin<Workspace> workspace = await this._git.CreateWorkspace(branch, this._baseBranch, cancellationToken);
        Fin<string> report = await workspace.Then(created => this.SpecifyIn(id, created, cancellationToken));
        Fin<Unit> removed = await workspace.Then(
            created => this._git.RemoveWorkspace(created, cancellationToken)
        );
        return report.Bind(message => removed.Map(_ => message));
    }

    private static string DescribeWaiting(WorkItemId id, AwaitingAnswers awaiting) =>
        $"{id} is waiting on answers to {awaiting.Questions.Count} question(s).";

    private static string DescribeStillWaiting(WorkItemId id) =>
        $"{id} is still waiting on a maintainer's answer; nothing was run.";

    public Task<Fin<IReadOnlyList<WorkItemId>>> ListAwaitingRevision(CancellationToken cancellationToken) =>
        this._changes.ListAwaitingRevision(cancellationToken);

    public async Task<Fin<string>> Revise(WorkItemId id, CancellationToken cancellationToken)
    {
        Fin<ProposalReview> review = await this._changes.ReadReview(id, cancellationToken);
        Fin<Workspace> workspace = await review.Then(found => this._git.OpenWorkspace(found.Branch, cancellationToken));
        Fin<string> report = await review.Then(
            found => workspace.Then(opened => this.ReviseIn(new Revision(found, opened), cancellationToken))
        );
        Fin<Unit> removed = await workspace.Then(opened => this._git.RemoveWorkspace(opened, cancellationToken));
        return report.Bind(message => removed.Map(_ => message));
    }

    private static string DescribeProposal(WorkItemId id) =>
        $"""
        The specifier role wrote this specification for {id}.

        It defines what the tests and the code will be held to, so it needs a
        maintainer's approval before it merges. Check the **Decisions** section
        first: it lists every gap the specifier closed on its own.
        """;

    private async Task<Fin<string>> SpecifyIn(
        WorkItemId id,
        Workspace workspace,
        CancellationToken cancellationToken
    )
    {
        Fin<SpecifyOutcome> outcome = await this._stage.Run(id, workspace.Path, cancellationToken);
        return await outcome.Then(result => this.Conclude(id, workspace, result, cancellationToken));
    }

    private Task<Fin<string>> Conclude(
        WorkItemId id,
        Workspace workspace,
        SpecifyOutcome outcome,
        CancellationToken cancellationToken
    ) =>
        outcome.Match(
            specified => this.Propose(id, workspace, specified, cancellationToken),
            awaiting => Task.FromResult(Fin.Succ(DescribeWaiting(id, awaiting))),
            _ => Task.FromResult(Fin.Succ(DescribeStillWaiting(id)))
        );

    private async Task<Fin<string>> Propose(
        WorkItemId id,
        Workspace workspace,
        Specified specified,
        CancellationToken cancellationToken
    )
    {
        string title = await TitleOf(id, workspace, specified, cancellationToken);
        Fin<Unit> committed = await this._git.Commit(
            workspace,
            [specified.SpecificationPath],
            title,
            cancellationToken
        );
        Fin<Unit> pushed = await committed.Then(_ => this._git.Push(workspace, cancellationToken));
        ChangeProposal proposal = new(id, workspace.Branch, title, DescribeProposal(id));
        Fin<string> proposed = await pushed.Then(_ => this._changes.Propose(proposal, cancellationToken));
        Fin<Unit> marked = await proposed.Then(_ => this._work.MarkSpecified(id, cancellationToken));
        return proposed.Bind(
            address => marked.Map(_ => $"Proposed the specification for {id} for review: {address}")
        );
    }

    private static async Task<string> TitleOf(
        WorkItemId id,
        Workspace workspace,
        Specified specified,
        CancellationToken cancellationToken
    )
    {
        string specification = await File.ReadAllTextAsync(
            Path.Combine(workspace.Path, specified.SpecificationPath),
            cancellationToken
        );
        return ProposalTitle.For(id, specification);
    }

    private async Task<Fin<string>> ReviseIn(Revision revision, CancellationToken cancellationToken)
    {
        Fin<SpecifyOutcome> outcome = await this._stage.Revise(
            revision.Review,
            revision.Workspace.Path,
            cancellationToken
        );
        return await outcome.Then(result => this.ConcludeRevision(revision, result, cancellationToken));
    }

    private Task<Fin<string>> ConcludeRevision(
        Revision revision,
        SpecifyOutcome outcome,
        CancellationToken cancellationToken
    ) =>
        outcome.Match(
            specified => this.PushRevision(revision, specified, cancellationToken),
            _ => this.ReplyAndReport(revision, QuestionsInstead, cancellationToken),
            _ => Task.FromResult(Fin.Succ(DescribeStillWaiting(revision.Review.Item)))
        );

    private async Task<Fin<string>> PushRevision(
        Revision revision,
        Specified specified,
        CancellationToken cancellationToken
    )
    {
        WorkItemId id = revision.Review.Item;
        string title = await TitleOf(id, revision.Workspace, specified, cancellationToken);
        Fin<Unit> committed = await this._git.Commit(
            revision.Workspace,
            [specified.SpecificationPath],
            $"Revise after review: {title}",
            cancellationToken
        );
        Fin<Unit> pushed = await committed.Then(_ => this._git.Push(revision.Workspace, cancellationToken));
        return await pushed.Then(_ => this.ReplyAndReport(revision, Revised, cancellationToken));
    }

    private async Task<Fin<string>> ReplyAndReport(
        Revision revision,
        string reply,
        CancellationToken cancellationToken
    )
    {
        Fin<Unit> replied = await this._changes.Reply(revision.Review, reply, cancellationToken);
        return replied.Map(_ => $"{revision.Review.Item}: {reply} ({revision.Review.Address})");
    }

    /// <summary>A proposal under revision, and the workspace holding its branch.</summary>
    private sealed record Revision(ProposalReview Review, Workspace Workspace);
}