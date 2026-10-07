using AgenticSoftwareWorkflow.Conductor.GitHub;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.GitHub;

public sealed class GitHubPullRequestsTest
{
    private const string Address = "https://github.com/owner/repository/pull/9";

    private const string OpenProposals = """
        [
          {
            "number": 9,
            "url": "https://github.com/owner/repository/pull/9",
            "body": "Please review.\n\n<!-- aswf-work-item: github:owner/repository#7 -->\n",
            "headRefName": "aswf/specify-7",
            "reviewDecision": "CHANGES_REQUESTED",
            "commits": [ { "committedDate": "2026-10-07T10:00:00Z" } ],
            "reviews": [
              {
                "author": { "login": "maintainer" },
                "body": "Make it general.",
                "state": "CHANGES_REQUESTED",
                "submittedAt": "2026-10-07T11:00:00Z"
              }
            ],
            "comments": []
          },
          {
            "number": 10,
            "url": "https://github.com/owner/repository/pull/10",
            "body": "Please review.\n\n<!-- aswf-work-item: github:owner/repository#8 -->\n",
            "headRefName": "aswf/specify-8",
            "reviewDecision": "REVIEW_REQUIRED",
            "commits": [ { "committedDate": "2026-10-07T10:00:00Z" } ],
            "reviews": [],
            "comments": []
          }
        ]
        """;

    private const string BehindAndClean = """
        [
          { "number": 9, "url": "https://github.com/owner/repository/pull/9", "mergeStateStatus": "BEHIND" },
          { "number": 10, "url": "https://github.com/owner/repository/pull/10", "mergeStateStatus": "CLEAN" }
        ]
        """;

    private const string LineComments = """
        [
          {
            "user": { "login": "maintainer" },
            "body": "Say which files.",
            "path": "spec/7-add-a-clock.md",
            "line": 12,
            "created_at": "2026-10-07T11:00:01Z"
          }
        ]
        """;

    private static readonly WorkItemId Seven = new("github:owner/repository", "7");
    private static readonly GitHubOptions Options = new("owner/repository", ["maintainer"], "/repository");

    private static readonly ChangeProposal Proposal = new(
        Seven,
        ProposalKind.Specification,
        "aswf/specify-7",
        "Specify: a clock (#7)",
        "Please review."
    );

    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();

    [Fact]
    public async Task Propose_WhenCalled_OpensAPullRequestTiedToItsItemAndAsksItToMergeOnceAllowed()
    {
        // Arrange
        this.Responds(Succeeded($"{Address}\n"), Succeeded(""));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<string> proposed = await pullRequests.Propose(Proposal, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Address, AssertSuccess(proposed));
        Assert.Equal(
            [
                (
                    "pr create --repo owner/repository --base master --head aswf/specify-7 "
                    + "--title Specify: a clock (#7) --body-file -",
                    "Please review.\n\n<!-- aswf-work-item: github:owner/repository#7 -->\n"
                ),
                ($"pr merge {Address} --auto --merge", ""),
            ],
            this.Commands()
        );
    }

    [Fact]
    public async Task Propose_WhenThePullRequestCannotBeCreated_DoesNotAskToMerge()
    {
        // Arrange
        this.Responds(new ProcessOutcome(1, "", "no commits", false));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<string> proposed = await pullRequests.Propose(Proposal, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<CommandFailed>(AssertFailure(proposed));
        _ = Assert.Single(this.Commands());
    }

    [Fact]
    public async Task Propose_WhenAutoMergeCannotBeRequested_Fails()
    {
        // Arrange
        this.Responds(Succeeded(Address), new ProcessOutcome(1, "", "auto-merge is disabled", false));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<string> proposed = await pullRequests.Propose(Proposal, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "'gh pr merge' exited with code 1: auto-merge is disabled",
            AssertFailure(proposed).Message
        );
    }

    [Fact]
    public async Task ListAwaitingRevision_WhenAMaintainerReviewedAfterTheLatestCommit_ListsThatItem()
    {
        // Arrange
        this.Responds(Succeeded(OpenProposals));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<IReadOnlyList<WorkItemId>> awaiting = await pullRequests.ListAwaitingRevision(
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([Seven], AssertSuccess(awaiting));
    }

    [Fact]
    public async Task ListAwaitingRevision_WhenCalled_ListsOnlyTheWorkersOwnOpenPullRequests()
    {
        // Arrange
        this.Responds(Succeeded("[]"));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        _ = await pullRequests.ListAwaitingRevision(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "pr list --repo owner/repository --state open --author @me --json "
            + "number,url,body,headRefName,reviewDecision,mergeStateStatus,reviews,comments,commits --limit 30",
            Assert.Single(this.Commands()).Arguments
        );
    }

    [Fact]
    public async Task ReadReview_WhenTheItemHasAProposal_ReturnsItsBranchAndEveryPieceOfFeedbackInOrder()
    {
        // Arrange
        this.Responds(Succeeded(OpenProposals), Succeeded(LineComments));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<ProposalReview> review = await pullRequests.ReadReview(Seven, TestContext.Current.CancellationToken);

        // Assert
        ProposalReview read = AssertSuccess(review);
        Assert.Equal(
            (
                Address,
                "aswf/specify-7",
                "Requested changes. Make it general.|On `spec/7-add-a-clock.md` line 12: Say which files."
            ),
            (read.Address, read.Branch, string.Join('|', read.Feedback.Select(comment => comment.Body)))
        );
    }

    [Fact]
    public async Task ReadReview_WhenReadingTheLineComments_AsksForThatPullRequestsComments()
    {
        // Arrange
        this.Responds(Succeeded(OpenProposals), Succeeded("[]"));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        _ = await pullRequests.ReadReview(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("api repos/owner/repository/pulls/9/comments", this.Commands()[1].Arguments);
    }

    [Fact]
    public async Task ReadReview_WhenTheItemHasNoOpenProposal_FailsWithProposalNotFound()
    {
        // Arrange
        this.Responds(Succeeded(OpenProposals));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<ProposalReview> review = await pullRequests.ReadReview(
            new WorkItemId("github:owner/repository", "99"),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("There is no open proposal for github:owner/repository#99.", AssertFailure(review).Message);
    }

    [Fact]
    public async Task ReadReview_WhenThePullRequestHasNoUrlOrBranch_LeavesThemEmpty()
    {
        // Arrange
        const string bare = """[ { "number": 9, "body": "<!-- aswf-work-item: github:owner/repository#7 -->" } ]""";
        this.Responds(Succeeded(bare), Succeeded("[]"));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<ProposalReview> review = await pullRequests.ReadReview(Seven, TestContext.Current.CancellationToken);

        // Assert
        ProposalReview read = AssertSuccess(review);
        Assert.Equal(("", ""), (read.Address, read.Branch));
    }

    [Fact]
    public async Task Reply_WhenCalled_CommentsOnThePullRequest()
    {
        // Arrange
        this.Responds(Succeeded(""));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");
        ProposalReview review = new(Seven, Address, "aswf/specify-7", []);

        // Act
        Fin<Unit> replied = await pullRequests.Reply(review, "Revised.", TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(replied);
        Assert.Equal(
            ($"pr comment {Address} --repo owner/repository --body-file -", "Revised."),
            Assert.Single(this.Commands())
        );
    }

    [Fact]
    public async Task UpdateBehind_WhenAProposalIsBehind_UpdatesOnlyThatOne()
    {
        // Arrange
        this.Responds(Succeeded(BehindAndClean), Succeeded(""));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        _ = await pullRequests.UpdateBehind(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("pr update-branch 9 --repo owner/repository", this.Commands()[^1].Arguments);
    }

    [Fact]
    public async Task UpdateBehind_WhenAProposalIsUpdated_ReportsIt()
    {
        // Arrange
        this.Responds(Succeeded(BehindAndClean), Succeeded(""));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<IReadOnlyList<string>> reports = await pullRequests.UpdateBehind(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([$"Brought {Address} up to date with master."], AssertSuccess(reports));
    }

    [Fact]
    public async Task UpdateBehind_WhenAProposalCannotBeUpdated_ReportsWhy()
    {
        // Arrange
        this.Responds(Succeeded(BehindAndClean), new ProcessOutcome(1, "", "merge conflict", false));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");

        // Act
        Fin<IReadOnlyList<string>> reports = await pullRequests.UpdateBehind(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [$"Could not bring {Address} up to date: 'gh pr update-branch' exited with code 1: merge conflict"],
            AssertSuccess(reports)
        );
    }

    [Fact]
    public async Task Propose_WhenItIsAnImplementation_TiesItWithABuildMarkerAndLeavesTheMergeToAMaintainer()
    {
        // Arrange
        this.Responds(Succeeded($"{Address}\n"));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");
        ChangeProposal build = Proposal with { Kind = ProposalKind.Implementation, Branch = "aswf/build-7" };

        // Act
        _ = await pullRequests.Propose(build, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "Please review.\n\n<!-- aswf-build: github:owner/repository#7 -->\n",
            Assert.Single(this.Commands()).Input
        );
    }

    private static ProcessOutcome Succeeded(string output) => new(0, output, "", false);

    private void Responds(ProcessOutcome first, params ProcessOutcome[] rest) =>
        this._processes.Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>()).Returns(first, rest);

    private List<(string Arguments, string Input)> Commands() =>
        [
            .. this._processes
                .ReceivedCalls()
                .Select(call => (ProcessRequest)call.GetArguments()[0]!)
                .Select(request => (string.Join(' ', request.Arguments), request.StandardInput)),
        ];
}