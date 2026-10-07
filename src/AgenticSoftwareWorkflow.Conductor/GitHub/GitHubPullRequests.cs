using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// Proposes changes as GitHub pull requests, with auto-merge requested: each
/// merges as soon as the repository's rules are met — immediately once the
/// gates pass for ordinary changes, and only after a maintainer's approval
/// for changes the rules reserve to them, such as specifications.
/// </summary>
public sealed class GitHubPullRequests(IProcessCapable processes, GitHubOptions options, string baseBranch)
    : IChangeProposing
{
    private const string ReadFromStandardInput = "-";
    private const string OwnPullRequests = "@me";
    private const string ListFields =
        "number,url,body,headRefName,reviewDecision,mergeStateStatus,reviews,comments,commits";
    private const string Behind = "BEHIND";
    // GitHub caps how many records one query may touch, and each pull request
    // brings its commits and their authors: 30 open proposals stays well within.
    private const string ListLimit = "30";

    private readonly CommandLineTool _gh = new(processes, options.Executable, options.WorkingDirectory);
    private readonly GitHubOptions _options = options;
    private readonly string _baseBranch = baseBranch;

    public async Task<Fin<string>> Propose(ChangeProposal proposal, CancellationToken cancellationToken)
    {
        Fin<string> created = await this._gh.Run(
            [
                "pr",
                "create",
                "--repo",
                this._options.Repository,
                "--base",
                this._baseBranch,
                "--head",
                proposal.Branch,
                "--title",
                proposal.Title,
                "--body-file",
                ReadFromStandardInput,
            ],
            $"{proposal.Description}\n\n{PullRequestFeedback.Marker(proposal.Item, proposal.Kind)}\n",
            cancellationToken
        );
        Fin<string> address = created.Map(output => output.Trim());
        Fin<string> merging = proposal.Kind == ProposalKind.Specification
            ? await address.Then(url => this._gh.Run(["pr", "merge", url, "--auto", "--merge"], cancellationToken))
            : address;
        return merging.Bind(_ => address);
    }

    public async Task<Fin<IReadOnlyList<WorkItemId>>> ListAwaitingRevision(CancellationToken cancellationToken)
    {
        Fin<List<GitHubPullRequest>> open = await this.OpenProposals(cancellationToken);
        return open.Map(this.AwaitingRevision);
    }

    public async Task<Fin<IReadOnlyList<WorkItemId>>> ListOpenSpecifications(CancellationToken cancellationToken)
    {
        Fin<List<GitHubPullRequest>> open = await this.OpenProposals(cancellationToken);
        return open.Map(IReadOnlyList<WorkItemId> (pullRequests) => [.. pullRequests.SelectMany(ItemsOf)]);
    }

    public async Task<Fin<ProposalReview>> ReadReview(WorkItemId id, CancellationToken cancellationToken)
    {
        Fin<List<GitHubPullRequest>> open = await this.OpenProposals(cancellationToken);
        Fin<GitHubPullRequest> proposal = open.Bind(pullRequests => FindFor(pullRequests, id));
        Fin<List<GitHubLineComment>> lines = await proposal.Then(found => this.LineComments(found, cancellationToken));
        return proposal.Bind(found => lines.Map(comments => this.ToReview(id, found, comments)));
    }

    public async Task<Fin<Unit>> Reply(ProposalReview review, string message, CancellationToken cancellationToken)
    {
        Fin<string> commented = await this._gh.Run(
            ["pr", "comment", review.Address, "--repo", this._options.Repository, "--body-file", ReadFromStandardInput],
            message,
            cancellationToken
        );
        return commented.Map(_ => Unit.Default);
    }

    public async Task<Fin<IReadOnlyList<string>>> UpdateBehind(CancellationToken cancellationToken)
    {
        Fin<List<GitHubPullRequest>> open = await this.OpenProposals(cancellationToken);
        return await open.Then(
            pullRequests => this.UpdateEach(
                [.. pullRequests.Where(pullRequest => pullRequest.MergeStateStatus == Behind)],
                cancellationToken
            )
        );
    }

    private static Fin<GitHubPullRequest> FindFor(List<GitHubPullRequest> pullRequests, WorkItemId id) =>
        pullRequests.Find(pullRequest => PullRequestFeedback.ItemOf(pullRequest) == id) is { } found
            ? Fin.Succ(found)
            : Fin.Fail<GitHubPullRequest>(new ProposalNotFound(id));

    private static IEnumerable<WorkItemId> ItemsOf(GitHubPullRequest pullRequest) =>
        PullRequestFeedback.ItemOf(pullRequest).Match<IEnumerable<WorkItemId>>(Some: id => [id], None: () => []);

    private IReadOnlyList<WorkItemId> AwaitingRevision(List<GitHubPullRequest> pullRequests) =>
        [
            .. pullRequests
                .Where(pullRequest => PullRequestFeedback.AwaitsRevision(pullRequest, this._options.Maintainers))
                .SelectMany(ItemsOf),
        ];

    private ProposalReview ToReview(WorkItemId id, GitHubPullRequest pullRequest, List<GitHubLineComment> comments) =>
        new(
            id,
            pullRequest.Url ?? string.Empty,
            pullRequest.HeadRefName ?? string.Empty,
            PullRequestFeedback.Since(pullRequest, comments, this._options.Maintainers)
        );

    // One proposal failing to update (a conflict, say) must not stop the others,
    // so each outcome is reported rather than the first failure returned.
    private async Task<Fin<IReadOnlyList<string>>> UpdateEach(
        List<GitHubPullRequest> behind,
        CancellationToken cancellationToken
    )
    {
        List<string> reports = [];
        foreach (GitHubPullRequest pullRequest in behind)
        {
            Fin<string> updated = await this._gh.Run(
                ["pr", "update-branch", $"{pullRequest.Number}", "--repo", this._options.Repository],
                cancellationToken
            );
            reports.Add(
                updated.Match(
                    Succ: _ => $"Brought {pullRequest.Url} up to date with {this._baseBranch}.",
                    Fail: failure => $"Could not bring {pullRequest.Url} up to date: {failure.Message}"
                )
            );
        }

        return Fin.Succ<IReadOnlyList<string>>(reports);
    }

    private async Task<Fin<List<GitHubPullRequest>>> OpenProposals(CancellationToken cancellationToken)
    {
        Fin<string> listed = await this._gh.Run(
            [
                "pr",
                "list",
                "--repo",
                this._options.Repository,
                "--state",
                "open",
                "--author",
                OwnPullRequests,
                "--json",
                ListFields,
                "--limit",
                ListLimit,
            ],
            cancellationToken
        );
        return listed.Bind(GitHubIssueReader.ReadPullRequests);
    }

    private async Task<Fin<List<GitHubLineComment>>> LineComments(
        GitHubPullRequest pullRequest,
        CancellationToken cancellationToken
    )
    {
        Fin<string> listed = await this._gh.Run(
            ["api", $"repos/{this._options.Repository}/pulls/{pullRequest.Number}/comments"],
            cancellationToken
        );
        return listed.Bind(GitHubIssueReader.ReadLineComments);
    }
}