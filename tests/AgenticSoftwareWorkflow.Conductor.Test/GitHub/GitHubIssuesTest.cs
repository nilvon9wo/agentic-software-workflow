using AgenticSoftwareWorkflow.Conductor.GitHub;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;
using NSubstitute;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.GitHub;

public sealed class GitHubIssuesTest
{
    private const string Repository = "owner/repository";
    private const string Source = "github:owner/repository";
    private const string Directory = "/repository";

    private const string Issue = """
        {
          "number": 7,
          "title": "Add a clock",
          "body": "Show the time.",
          "labels": [ { "name": "ready" } ],
          "comments": [
            { "author": { "login": "Maintainer" }, "body": "Use UTC.", "createdAt": "2026-10-06T15:55:19Z" },
            { "author": { "login": "repository-bot" }, "body": "Which zone?", "createdAt": "2026-10-06T15:50:00Z" },
            { "author": null, "body": "From a deleted account.", "createdAt": "2026-10-06T15:40:00Z" },
            { "author": { "login": "maintainer" }, "createdAt": "2026-10-06T16:00:00Z" }
          ]
        }
        """;

    private static readonly WorkItemId Seven = new(Source, "7");
    private static readonly GitHubOptions Options = new(Repository, ["maintainer"], Directory);

    private readonly IProcessCapable _processes = Substitute.For<IProcessCapable>();

    [Fact]
    public async Task ListReady_WhenIssuesAreReady_ListsThoseNotWaitingOnAHuman()
    {
        // Arrange
        this.Responds(
            Succeeded(
                """
                [
                  { "number": 3, "labels": [ { "name": "ready" } ] },
                  { "number": 5, "labels": [ { "name": "ready" }, { "name": "needs-human" } ] },
                  { "number": 8 }
                ]
                """
            )
        );
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<IReadOnlyList<WorkItemId>> ready = await issues.ListReady(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([new WorkItemId(Source, "3"), new WorkItemId(Source, "8")], AssertSuccess(ready));
    }

    [Fact]
    public async Task ListReady_WhenCalled_AsksGitHubForOpenReadyIssues()
    {
        // Arrange
        this.Responds(Succeeded("[]"));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        _ = await issues.ListReady(TestContext.Current.CancellationToken);

        // Assert
        ProcessRequest request = this.Requests().Single();
        Assert.Equal(
            (
                "gh",
                Directory,
                "issue list --repo owner/repository --label ready --state open --json number,labels --limit 100"
            ),
            (request.Executable, request.WorkingDirectory, string.Join(' ', request.Arguments))
        );
    }

    [Fact]
    public async Task ListReady_WhenTheCommandFails_FailsWithItsError()
    {
        // Arrange
        this.Responds(new ProcessOutcome(4, "", "not authenticated", false));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<IReadOnlyList<WorkItemId>> ready = await issues.ListReady(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new CommandFailed("gh issue list", 4, "not authenticated"), AssertFailure(ready));
    }

    [Fact]
    public async Task ListReady_WhenTheCommandTimesOut_FailsSayingSo()
    {
        // Arrange
        this.Responds(new ProcessOutcome(-1, "", "", true));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<IReadOnlyList<WorkItemId>> ready = await issues.ListReady(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("'gh issue list' exited with code -1: timed out", AssertFailure(ready).Message);
    }

    [Fact]
    public async Task ListReady_WhenTheResponseIsNotJson_FailsWithMalformedResponse()
    {
        // Arrange
        this.Responds(Succeeded("<html>"));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<IReadOnlyList<WorkItemId>> ready = await issues.ListReady(TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<WorkResponseMalformed>(AssertFailure(ready));
    }

    [Fact]
    public async Task Read_WhenTheIssueExists_ReturnsItAndItsConversation()
    {
        // Arrange
        this.Responds(Succeeded(Issue));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<WorkItem> read = await issues.Read(Seven, TestContext.Current.CancellationToken);

        // Assert
        WorkItem item = AssertSuccess(read);
        Assert.Equal(
            (Seven, "Add a clock", "Show the time.", "ready", 4),
            (item.Id, item.Title, item.Body, Assert.Single(item.Labels), item.Comments.Count)
        );
    }

    [Fact]
    public async Task Read_WhenOthersHaveCommented_TrustsOnlyMaintainersAndMarksTheWorkflowsOwn()
    {
        // Arrange
        this.Responds(Succeeded(Issue), Succeeded("repository-bot\n"));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<WorkItem> read = await issues.Read(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                new WorkComment("Maintainer", "Use UTC.", true, false),
                new WorkComment("repository-bot", "Which zone?", false, true),
                new WorkComment("ghost", "From a deleted account.", false, false),
                new WorkComment("maintainer", "", true, false),
            ],
            AssertSuccess(read).Comments
        );
    }

    [Fact]
    public async Task Read_WhenTheIssueIsLabelledNeedsHuman_IsWaiting()
    {
        // Arrange
        this.Responds(Succeeded("""{ "number": 7, "labels": [ { "name": "needs-human" } ] }"""));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<WorkItem> read = await issues.Read(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(AssertSuccess(read).IsWaiting);
    }

    [Fact]
    public async Task Read_WhenCalled_AsksGitHubWhichAccountTheWorkersUse()
    {
        // Arrange
        this.Responds(Succeeded(Issue), Succeeded("repository-bot\n"));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        _ = await issues.Read(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("api user --jq .login", string.Join(' ', this.Requests()[1].Arguments));
    }

    [Fact]
    public async Task Read_WhenTheWorkersAccountCannotBeFound_FailsWithThatError()
    {
        // Arrange
        this.Responds(Succeeded(Issue), new ProcessOutcome(1, "", "not logged in", false));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<WorkItem> read = await issues.Read(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<CommandFailed>(AssertFailure(read));
    }

    [Fact]
    public async Task Read_WhenTheIssueHasOnlyANumber_DefaultsEverythingElse()
    {
        // Arrange
        this.Responds(Succeeded("""{ "number": 7 }"""));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<WorkItem> read = await issues.Read(Seven, TestContext.Current.CancellationToken);

        // Assert
        WorkItem item = AssertSuccess(read);
        Assert.Equal(("", "", 0, 0), (item.Title, item.Body, item.Labels.Count, item.Comments.Count));
    }

    [Fact]
    public async Task Read_WhenTheResponseIsJsonNull_FailsWithMalformedResponse()
    {
        // Arrange
        this.Responds(Succeeded("null"));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<WorkItem> read = await issues.Read(Seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "The work source's response could not be read: the response was the JSON literal null",
            AssertFailure(read).Message
        );
    }

    [Fact]
    public async Task Read_WhenTheItemBelongsToAnotherSource_FailsWithoutCallingGitHub()
    {
        // Arrange
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<WorkItem> read = await issues.Read(
            new WorkItemId("jira:PROJECT", "7"),
            TestContext.Current.CancellationToken
        );

        // Assert
        _ = Assert.IsType<ForeignWorkItem>(AssertFailure(read));
        Assert.Empty(this.Requests());
    }

    [Fact]
    public async Task Ask_WhenCalled_PostsTheQuestionThenMarksTheIssueAsWaitingOnTheMaintainers()
    {
        // Arrange
        this.Responds(Succeeded(""), Succeeded(""));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<Unit> asked = await issues.Ask(Seven, "Which time zone?", TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(asked);
        Assert.Equal(
            [
                ("issue comment 7 --repo owner/repository --body-file -", "Which time zone?"),
                (
                    "issue edit 7 --repo owner/repository --add-label needs-human --add-assignee maintainer",
                    ""
                ),
            ],
            this.Requests().Select(request => (string.Join(' ', request.Arguments), request.StandardInput))
        );
    }

    [Fact]
    public async Task Ask_WhenPostingTheQuestionFails_DoesNotMarkTheIssue()
    {
        // Arrange
        this.Responds(new ProcessOutcome(1, "", "rate limited", false));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<Unit> asked = await issues.Ask(Seven, "Which time zone?", TestContext.Current.CancellationToken);

        // Assert
        _ = Assert.IsType<CommandFailed>(AssertFailure(asked));
        _ = Assert.Single(this.Requests());
    }

    [Fact]
    public async Task MarkSpecified_WhenCalled_ReplacesReadyWithSpecified()
    {
        // Arrange
        this.Responds(Succeeded(""));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<Unit> marked = await issues.MarkSpecified(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(marked);
        Assert.Equal(
            "issue edit 7 --repo owner/repository --remove-label ready --add-label specified",
            string.Join(' ', this.Requests().Single().Arguments)
        );
    }

    [Fact]
    public async Task Resolve_WhenCalled_RemovesTheWaitingLabelAndAssignment()
    {
        // Arrange
        this.Responds(Succeeded(""));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<Unit> resolved = await issues.Resolve(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(resolved);
        Assert.Equal(
            "issue edit 7 --repo owner/repository --remove-label needs-human --remove-assignee maintainer",
            string.Join(' ', this.Requests().Single().Arguments)
        );
    }

    [Fact]
    public async Task ListSpecified_WhenCalled_ListsTheOpenIssuesLabelledSpecified()
    {
        // Arrange
        this.Responds(Succeeded("[]"));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        _ = await issues.ListSpecified(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            "issue list --repo owner/repository --label specified --state open --json number,labels --limit 100",
            string.Join(' ', this.Requests().Single().Arguments)
        );
    }

    [Fact]
    public async Task MarkBuilt_WhenCalled_ReplacesSpecifiedWithBuilt()
    {
        // Arrange
        this.Responds(Succeeded(""));
        IWorkSupplying issues = new GitHubIssues(this._processes, Options);

        // Act
        Fin<Unit> marked = await issues.MarkBuilt(Seven, TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(marked);
        Assert.Equal(
            "issue edit 7 --repo owner/repository --remove-label specified --add-label built",
            string.Join(' ', this.Requests().Single().Arguments)
        );
    }

    private static ProcessOutcome Succeeded(string standardOutput) => new(0, standardOutput, "", false);

    private void Responds(ProcessOutcome first, params ProcessOutcome[] rest) =>
        this._processes
            .Run(Arg.Any<ProcessRequest>(), Arg.Any<CancellationToken>())
            .Returns(first, rest);

    private List<ProcessRequest> Requests() =>
        [.. this._processes.ReceivedCalls().Select(call => (ProcessRequest)call.GetArguments()[0]!)];
}