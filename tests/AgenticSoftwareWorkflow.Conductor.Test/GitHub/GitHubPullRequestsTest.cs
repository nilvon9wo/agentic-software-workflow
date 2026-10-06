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

    private static readonly GitHubOptions Options = new("owner/repository", ["maintainer"], "/repository");
    private static readonly ChangeProposal Proposal = new("aswf/specify-7", "Specification for 7", "Please review.");

    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();

    [Fact]
    public async Task Propose_WhenCalled_OpensAPullRequestAndAsksItToMergeOnceAllowed()
    {
        // Arrange
        this.Responds(Succeeded($"{Address}\n"), Succeeded(""));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");
        Fin<string> proposed;

        // Act
        proposed = await pullRequests.Propose(Proposal, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Address, AssertSuccess(proposed));
        Assert.Equal(
            [
                (
                    "pr create --repo owner/repository --base master --head aswf/specify-7 "
                    + "--title Specification for 7 --body-file -",
                    "Please review."
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
        Fin<string> proposed;

        // Act
        proposed = await pullRequests.Propose(Proposal, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((CommandErrors.FailedCode, 1), (AssertFailure(proposed).Code, this.Commands().Count));
    }

    [Fact]
    public async Task Propose_WhenAutoMergeCannotBeRequested_Fails()
    {
        // Arrange
        this.Responds(Succeeded(Address), new ProcessOutcome(1, "", "auto-merge is disabled", false));
        IChangeProposing pullRequests = new GitHubPullRequests(this._processes, Options, "master");
        Fin<string> proposed;

        // Act
        proposed = await pullRequests.Propose(Proposal, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "'gh pr merge' exited with code 1: auto-merge is disabled",
            AssertFailure(proposed).Message
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