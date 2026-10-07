using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// Reads a workflow pull request: which work item it is for (from a marker in
/// its description), whether it is approved, and what maintainers have said
/// since its latest commit — the feedback a revision must answer.
/// </summary>
/// <remarks>
/// Only maintainers count, by login, exactly as on issues. Feedback older than
/// the latest commit has been answered already: a revision is a new commit.
/// A maintainer's own commits do not count as answers — GitHub's "Update
/// branch", which merges the base in, is one.
/// </remarks>
internal static class PullRequestFeedback
{
    private const string MarkerStart = "<!-- aswf-work-item: ";
    private const string MarkerEnd = " -->";
    private const string Approved = "APPROVED";
    private const string ChangesRequested = "CHANGES_REQUESTED";

    /// <summary>The hidden line that ties a pull request to its work item.</summary>
    public static string Marker(WorkItemId id) => $"{MarkerStart}{id}{MarkerEnd}";

    public static Option<WorkItemId> ItemOf(GitHubPullRequest pullRequest)
    {
        string body = pullRequest.Body ?? string.Empty;
        int start = body.IndexOf(MarkerStart, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : body.IndexOf(MarkerEnd, start, StringComparison.Ordinal);
        return end < 0
            ? Option<WorkItemId>.None
            : WorkItemId.Parse(body[(start + MarkerStart.Length)..end]);
    }

    /// <summary>Not approved, and a maintainer has reviewed or commented since the latest commit.</summary>
    public static bool AwaitsRevision(GitHubPullRequest pullRequest, IReadOnlyList<string> maintainers)
    {
        DateTimeOffset changed = LastChanged(pullRequest, maintainers);
        bool isReviewedSince = (pullRequest.Reviews ?? [])
            .Any(review => IsMaintainers(review.Author, maintainers) && review.SubmittedAt > changed);
        bool isCommentedSince = (pullRequest.Comments ?? [])
            .Any(comment => IsMaintainers(comment.Author, maintainers) && comment.CreatedAt > changed);
        return pullRequest.ReviewDecision != Approved && (isReviewedSince || isCommentedSince);
    }

    /// <summary>
    /// Every maintainer's review summary, comment, and line comment since the
    /// latest commit, oldest first.
    /// </summary>
    public static IReadOnlyList<WorkComment> Since(
        GitHubPullRequest pullRequest,
        IReadOnlyList<GitHubLineComment> lineComments,
        IReadOnlyList<string> maintainers
    )
    {
        // Each `!` below follows a filter that kept only entries with an author and a time.
        DateTimeOffset changed = LastChanged(pullRequest, maintainers);
        IEnumerable<(DateTimeOffset At, WorkComment Comment)> reviews = (pullRequest.Reviews ?? [])
            .Where(review => IsMaintainers(review.Author, maintainers) && review.SubmittedAt > changed)
            .Where(review => review.State == ChangesRequested || !string.IsNullOrWhiteSpace(review.Body))
            .Select(
                review => (
                    review.SubmittedAt!.Value,
                    Feedback(review.Author!, DescribeReview(review.State, review.Body ?? string.Empty))
                )
            );
        IEnumerable<(DateTimeOffset At, WorkComment Comment)> comments = (pullRequest.Comments ?? [])
            .Where(comment => IsMaintainers(comment.Author, maintainers) && comment.CreatedAt > changed)
            .Select(comment => (comment.CreatedAt!.Value, Feedback(comment.Author!, comment.Body ?? string.Empty)));
        IEnumerable<(DateTimeOffset At, WorkComment Comment)> lines = lineComments
            .Where(line => IsMaintainers(line.User, maintainers) && line.CreatedAt > changed)
            .Select(line => (line.CreatedAt!.Value, Feedback(line.User!, DescribeLine(line))));
        return [.. reviews.Concat(comments).Concat(lines).OrderBy(entry => entry.At).Select(entry => entry.Comment)];
    }

    private static DateTimeOffset LastChanged(GitHubPullRequest pullRequest, IReadOnlyList<string> maintainers) =>
        (pullRequest.Commits ?? [])
            .Where(commit => !(commit.Authors ?? []).Any(author => IsMaintainers(author, maintainers)))
            .Select(commit => commit.CommittedDate ?? DateTimeOffset.MinValue)
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();

    private static bool IsMaintainers(GitHubAuthor? author, IReadOnlyList<string> maintainers) =>
        author is not null && maintainers.Contains(author.Login, StringComparer.OrdinalIgnoreCase);

    private static WorkComment Feedback(GitHubAuthor author, string body) => new(author.Login, body, true, false);

    private static string DescribeReview(string? state, string body) =>
        state == ChangesRequested
            ? $"Requested changes. {body}".TrimEnd()
            : body;

    private static string DescribeLine(GitHubLineComment line) =>
        $"On `{line.Path}` line {line.Line}: {line.Body}";
}