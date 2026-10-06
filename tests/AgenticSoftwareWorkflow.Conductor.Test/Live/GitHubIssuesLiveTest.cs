using AgenticSoftwareWorkflow.Conductor.GitHub;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;
using static AgenticSoftwareWorkflow.Conductor.Test.Support.FinAssertions;

namespace AgenticSoftwareWorkflow.Conductor.Test.Live;

/// <summary>
/// Reads this repository's real issues through <c>gh</c>, proving the adapter
/// speaks the CLI's actual JSON rather than only the shapes its unit tests
/// assume. Read-only: nothing is posted or changed.
/// </summary>
/// <remarks>
/// Explicit: needs <c>gh</c> signed in (as the workers' account) and network
/// access. Run with scripts/live-checks.sh.
/// </remarks>
[Trait("Category", "Live")]
public sealed class GitHubIssuesLiveTest
{
    private const string Repository = "nilvon9wo/agentic-software-workflow";

    private static readonly GitHubOptions Options = new(Repository, ["nilvon9wo"], AppContext.BaseDirectory);

    [Fact(Explicit = true)]
    public async Task Read_WhenGivenARealIssue_ReadsItsTitleAndConversation()
    {
        // Arrange
        IWorkSupplying issues = new GitHubIssues(new SystemProcessRunner(), Options);
        WorkItemId seven = new($"github:{Repository}", "7");

        // Act
        Fin<WorkItem> read = await issues.Read(seven, TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("Pipeline stage 1", AssertSuccess(read).Title, StringComparison.Ordinal);
    }

    [Fact(Explicit = true)]
    public async Task ListReady_WhenCalledAgainstTheRealRepository_Succeeds()
    {
        // Arrange
        IWorkSupplying issues = new GitHubIssues(new SystemProcessRunner(), Options);

        // Act
        Fin<IReadOnlyList<WorkItemId>> ready = await issues.ListReady(TestContext.Current.CancellationToken);

        // Assert
        _ = AssertSuccess(ready);
    }
}