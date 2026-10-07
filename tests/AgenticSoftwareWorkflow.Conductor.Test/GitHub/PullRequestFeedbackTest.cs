using AgenticSoftwareWorkflow.Conductor.GitHub;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.Test.GitHub;

public sealed class PullRequestFeedbackTest
{
    private static readonly IReadOnlyList<string> Maintainers = ["maintainer"];
    private static readonly WorkItemId Seven = new("github:owner/repository", "7");
    private static readonly DateTimeOffset Committed = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly GitHubAuthor Maintainer = new("Maintainer");
    private static readonly GitHubAuthor Stranger = new("stranger");

    [Fact]
    public void ItemOf_WhenTheDescriptionHasTheMarker_ReturnsTheItem()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with { Body = $"Text.\n\n{PullRequestFeedback.Marker(Seven)}\n" };

        // Act
        Option<WorkItemId> item = PullRequestFeedback.ItemOf(pullRequest);

        // Assert
        Assert.Equal(Prelude.Some(Seven), item);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("No marker here.")]
    [InlineData("<!-- aswf-work-item: github:owner/repository#7")]
    [InlineData("<!-- aswf-work-item: not-an-id -->")]
    public void ItemOf_WhenTheDescriptionHasNoWholeMarker_ReturnsNone(string? body)
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with { Body = body };

        // Act
        Option<WorkItemId> item = PullRequestFeedback.ItemOf(pullRequest);

        // Assert
        Assert.True(item.IsNone);
    }

    [Fact]
    public void AwaitsRevision_WhenAMaintainerReviewedAfterTheLatestCommit_IsTrue()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with { Reviews = [Review(Maintainer, "COMMENTED", "Why?")] };

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.True(awaits);
    }

    [Fact]
    public void AwaitsRevision_WhenAMaintainerCommentedAfterTheLatestCommit_IsTrue()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with { Comments = [Comment(Maintainer, "Why?")] };

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.True(awaits);
    }

    [Fact]
    public void AwaitsRevision_WhenTheFeedbackPredatesTheLatestCommit_IsFalse()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Reviews = [Review(Maintainer, "CHANGES_REQUESTED", "Why?") with { SubmittedAt = Committed.AddMinutes(-1) }],
            Comments = [Comment(Maintainer, "Why?") with { CreatedAt = Committed.AddMinutes(-1) }],
        };

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.False(awaits);
    }

    [Fact]
    public void AwaitsRevision_WhenOnlyAStrangerCommented_IsFalse()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Reviews = [Review(Stranger, "CHANGES_REQUESTED", "Rewrite it.")],
            Comments = [Comment(Stranger, "Rewrite it."), Comment(null, "Deleted account.")],
        };

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.False(awaits);
    }

    [Fact]
    public void AwaitsRevision_WhenTheProposalIsApproved_IsFalse()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            ReviewDecision = "APPROVED",
            Comments = [Comment(Maintainer, "Apply this generally later.")],
        };

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.False(awaits);
    }

    [Fact]
    public void AwaitsRevision_WhenThereAreNoCommitsReviewsOrComments_IsFalse()
    {
        // Arrange
        GitHubPullRequest pullRequest = new(9, null, null, null, null, null, null, null);

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.False(awaits);
    }

    [Fact]
    public void Since_WhenMaintainersGaveFeedback_ReturnsEachPieceOldestFirst()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Reviews =
            [
                Review(Maintainer, "CHANGES_REQUESTED", "Make it general.") with
                {
                    SubmittedAt = Committed.AddMinutes(3),
                },
            ],
            Comments = [Comment(Maintainer, "First thought.")],
        };
        GitHubLineComment line = new(Maintainer, "Name the files.", "spec/7.md", 12, Committed.AddMinutes(2));

        // Act
        IReadOnlyList<WorkComment> feedback = PullRequestFeedback.Since(pullRequest, [line], Maintainers);

        // Assert
        Assert.Equal(
            [
                new WorkComment("Maintainer", "First thought.", true, false),
                new WorkComment("Maintainer", "On `spec/7.md` line 12: Name the files.", true, false),
                new WorkComment("Maintainer", "Requested changes. Make it general.", true, false),
            ],
            feedback
        );
    }

    [Fact]
    public void Since_WhenAReviewOnlyRequestedChanges_SaysSo()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Reviews = [Review(Maintainer, "CHANGES_REQUESTED", null)],
        };

        // Act
        IReadOnlyList<WorkComment> feedback = PullRequestFeedback.Since(pullRequest, [], Maintainers);

        // Assert
        Assert.Equal("Requested changes.", Assert.Single(feedback).Body);
    }

    [Fact]
    public void Since_WhenAReviewOnlyHoldsLineComments_LeavesTheEmptySummaryOut()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with { Reviews = [Review(Maintainer, "COMMENTED", "")] };

        // Act
        IReadOnlyList<WorkComment> feedback = PullRequestFeedback.Since(pullRequest, [], Maintainers);

        // Assert
        Assert.Empty(feedback);
    }

    [Fact]
    public void Since_WhenACommentHasNoBody_KeepsItAsEmpty()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with { Comments = [Comment(Maintainer, null)] };

        // Act
        IReadOnlyList<WorkComment> feedback = PullRequestFeedback.Since(pullRequest, [], Maintainers);

        // Assert
        Assert.Equal("", Assert.Single(feedback).Body);
    }

    [Fact]
    public void Since_WhenSomeFeedbackIsOldOrFromStrangers_LeavesThatOut()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Reviews =
            [
                Review(Stranger, "COMMENTED", "Rewrite."),
                Review(Maintainer, "COMMENTED", "Old.") with { SubmittedAt = Committed.AddMinutes(-1) },
            ],
            Comments =
            [
                Comment(Stranger, "Rewrite."),
                Comment(Maintainer, "Old.") with { CreatedAt = Committed.AddMinutes(-1) },
            ],
        };
        GitHubLineComment strangers = new(Stranger, "Rewrite.", "spec/7.md", 1, Committed.AddMinutes(1));
        GitHubLineComment old = new(Maintainer, "Old.", "spec/7.md", 1, Committed.AddMinutes(-1));

        // Act
        IReadOnlyList<WorkComment> feedback = PullRequestFeedback.Since(pullRequest, [strangers, old], Maintainers);

        // Assert
        Assert.Empty(feedback);
    }

    [Fact]
    public void AwaitsRevision_WhenFeedbackHasNoAuthorOrNoTime_IsFalse()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Reviews = [new GitHubReview(null, "Why?", "COMMENTED", Committed.AddMinutes(1)), Untimed()],
            Comments = [Comment(Maintainer, "Why?") with { CreatedAt = null }],
        };

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.False(awaits);
    }

    [Fact]
    public void AwaitsRevision_WhenTheCommitHasNoDate_CountsAllOfAMaintainersFeedback()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Commits = [new GitHubCommit(null, null)],
            Comments = [Comment(Maintainer, "Why?") with { CreatedAt = Committed.AddYears(-1) }],
        };

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.True(awaits);
    }

    [Fact]
    public void Since_WhenFeedbackHasNoAuthorOrNoTime_LeavesItOut()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Reviews = [new GitHubReview(null, "Why?", "COMMENTED", Committed.AddMinutes(1)), Untimed()],
            Comments = [Comment(Maintainer, "Why?") with { CreatedAt = null }],
        };
        GitHubLineComment anonymous = new(null, "Why?", "spec/7.md", 1, Committed.AddMinutes(1));
        GitHubLineComment untimed = new(Maintainer, "Why?", "spec/7.md", 1, null);

        // Act
        IReadOnlyList<WorkComment> feedback = PullRequestFeedback.Since(pullRequest, [anonymous, untimed], Maintainers);

        // Assert
        Assert.Empty(feedback);
    }

    [Fact]
    public void Since_WhenAMaintainerCommentedInAReview_KeepsTheSummaryAsWritten()
    {
        // Arrange
        GitHubPullRequest pullRequest = PullRequest() with { Reviews = [Review(Maintainer, "COMMENTED", "Why?")] };

        // Act
        IReadOnlyList<WorkComment> feedback = PullRequestFeedback.Since(pullRequest, [], Maintainers);

        // Assert
        Assert.Equal("Why?", Assert.Single(feedback).Body);
    }

    [Fact]
    public void AwaitsRevision_WhenAMaintainerOnlyUpdatedTheBranchSinceReviewing_IsStillTrue()
    {
        // Arrange
        GitHubCommit updateBranch = new(Committed.AddMinutes(5), [Maintainer]);
        GitHubPullRequest pullRequest = PullRequest() with
        {
            Commits = [new GitHubCommit(Committed, [new GitHubAuthor("workers-bot")]), updateBranch],
            Reviews = [Review(Maintainer, "CHANGES_REQUESTED", "Use GlobalSuppressions.cs.")],
        };

        // Act
        bool awaits = PullRequestFeedback.AwaitsRevision(pullRequest, Maintainers);

        // Assert
        Assert.True(awaits);
    }

    private static GitHubPullRequest PullRequest() =>
        new(
            9,
            "https://example.com/pull/9",
            null,
            "aswf/specify-7",
            "REVIEW_REQUIRED",
            [],
            [],
            [new GitHubCommit(Committed, [new GitHubAuthor("workers-bot")])]
        );

    /// <summary>A review a minute after the commit; <c>with</c> moves it.</summary>
    private static GitHubReview Review(GitHubAuthor author, string state, string? body) =>
        new(author, body, state, Committed.AddMinutes(1));

    /// <summary>A comment a minute after the commit; <c>with</c> moves it.</summary>
    private static GitHubComment Comment(GitHubAuthor? author, string? body) =>
        new(author, body, Committed.AddMinutes(1));

    /// <summary>A maintainer's review with no submission time.</summary>
    private static GitHubReview Untimed() => Review(Maintainer, "COMMENTED", "Why?") with { SubmittedAt = null };
}