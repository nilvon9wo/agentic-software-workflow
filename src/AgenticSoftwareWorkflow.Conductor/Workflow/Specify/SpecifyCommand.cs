using AgenticSoftwareWorkflow.Conductor.Agents;
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
    IWorkSupplying work,
    IAgentic agent,
    GitRepository git,
    IChangeProposing changes,
    string baseBranch
)
{
    private const string BranchPrefix = "aswf/specify-";

    private readonly SpecifyStage _stage = new(work, agent);
    private readonly GitRepository _git = git;
    private readonly IChangeProposing _changes = changes;
    private readonly string _baseBranch = baseBranch;

    /// <summary>What happened, in a sentence fit for a person to read.</summary>
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
        Fin<Unit> committed = await this._git.Commit(
            workspace,
            [specified.SpecificationPath],
            $"Specify {id}",
            cancellationToken
        );
        Fin<Unit> pushed = await committed.Then(_ => this._git.Push(workspace, cancellationToken));
        ChangeProposal proposal = new(workspace.Branch, $"Specification for {id}", DescribeProposal(id));
        Fin<string> proposed = await pushed.Then(_ => this._changes.Propose(proposal, cancellationToken));
        return proposed.Map(address => $"Proposed the specification for {id} for review: {address}");
    }
}