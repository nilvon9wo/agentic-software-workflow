using System.Globalization;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// GitHub Issues as a work source, through the GitHub CLI. An issue labelled
/// <c>ready</c> is ready; <c>needs-human</c>, plus an assignee, marks one
/// waiting on a maintainer.
/// </summary>
public sealed class GitHubIssues(IProcessCapable processes, GitHubOptions options) : IWorkSupplying
{
    private const string ReadyLabel = "ready";
    private const string NeedsHumanLabel = "needs-human";
    private const string UnknownAuthor = "ghost";
    private const string ListFields = "number,labels";
    private const string ViewFields = "number,title,body,labels,comments";
    private const string IssueListLimit = "100";
    private const string ReadFromStandardInput = "-";
    private const string ListSeparator = ",";
    private const int TimedOutExitCode = -1;

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);

    private readonly IProcessCapable _processes = processes;
    private readonly GitHubOptions _options = options;

    private string Source => $"github:{this._options.Repository}";

    public async Task<Fin<IReadOnlyList<WorkItemId>>> ReadyAsync(CancellationToken cancellationToken)
    {
        Fin<string> listed = await this.RunAsync(
            [
                "issue",
                "list",
                "--repo",
                this._options.Repository,
                "--label",
                ReadyLabel,
                "--state",
                "open",
                "--json",
                ListFields,
                "--limit",
                IssueListLimit,
            ],
            string.Empty,
            cancellationToken
        );
        return listed.Bind(GitHubIssueReader.ReadIssues).Map(this.NotWaiting);
    }

    public async Task<Fin<WorkItem>> ReadAsync(WorkItemId id, CancellationToken cancellationToken)
    {
        Fin<string> viewed = await ThenAsync(
            this.RequireOwn(id),
            () => this.RunAsync(
                ["issue", "view", id.Key, "--repo", this._options.Repository, "--json", ViewFields],
                string.Empty,
                cancellationToken
            )
        );
        return viewed.Bind(GitHubIssueReader.ReadIssue).Map(this.ToWorkItem);
    }

    public async Task<Fin<Unit>> AskAsync(WorkItemId id, string question, CancellationToken cancellationToken)
    {
        Fin<string> commented = await ThenAsync(
            this.RequireOwn(id),
            () => this.RunAsync(
                ["issue", "comment", id.Key, "--repo", this._options.Repository, "--body-file", ReadFromStandardInput],
                question,
                cancellationToken
            )
        );
        Fin<string> marked = await ThenAsync(
            commented,
            () => this.EditAsync(id, "--add-label", "--add-assignee", cancellationToken)
        );
        return marked.Map(_ => Unit.Default);
    }

    public async Task<Fin<Unit>> ResolveAsync(WorkItemId id, CancellationToken cancellationToken)
    {
        Fin<string> unmarked = await ThenAsync(
            this.RequireOwn(id),
            () => this.EditAsync(id, "--remove-label", "--remove-assignee", cancellationToken)
        );
        return unmarked.Map(_ => Unit.Default);
    }

    /// <summary>Runs the next step only if the previous one succeeded.</summary>
    private static Task<Fin<string>> ThenAsync<TPrevious>(Fin<TPrevious> previous, Func<Task<Fin<string>>> next) =>
        previous.Match(
            Succ: _ => next(),
            Fail: error => Task.FromResult(Fin.Fail<string>(error))
        );

    private static bool IsWaiting(GitHubIssue issue) =>
        (issue.Labels ?? []).Any(label => label.Name == NeedsHumanLabel);

    private static Fin<string> ToOutput(ProcessOutcome outcome, string command) =>
        outcome switch
        {
            { HasTimedOut: true } => Fin.Fail<string>(
                WorkErrors.CommandFailed(command, TimedOutExitCode, "timed out")
            ),
            { ExitCode: 0 } => Fin.Succ(outcome.StandardOutput),
            _ => Fin.Fail<string>(WorkErrors.CommandFailed(command, outcome.ExitCode, outcome.StandardError)),
        };

    private IReadOnlyList<WorkItemId> NotWaiting(List<GitHubIssue> issues) =>
        [.. issues.Where(issue => !IsWaiting(issue)).Select(issue => this.IdOf(issue.Number))];

    private WorkItemId IdOf(int number) =>
        new(this.Source, number.ToString(CultureInfo.InvariantCulture));

    private Fin<WorkItemId> RequireOwn(WorkItemId id) =>
        id.Source == this.Source
            ? Fin.Succ(id)
            : Fin.Fail<WorkItemId>(WorkErrors.ForeignItem(id, this.Source));

    private WorkItem ToWorkItem(GitHubIssue issue) =>
        new(
            this.IdOf(issue.Number),
            issue.Title ?? string.Empty,
            issue.Body ?? string.Empty,
            [.. (issue.Labels ?? []).Select(label => label.Name)],
            [.. (issue.Comments ?? []).Select(this.ToWorkComment)]
        );

    private WorkComment ToWorkComment(GitHubComment comment)
    {
        string author = comment.Author?.Login ?? UnknownAuthor;
        bool isTrusted = this._options.Maintainers.Contains(author, StringComparer.OrdinalIgnoreCase);
        return new WorkComment(author, comment.Body ?? string.Empty, isTrusted);
    }

    private Task<Fin<string>> EditAsync(
        WorkItemId id,
        string labelFlag,
        string assigneeFlag,
        CancellationToken cancellationToken
    ) =>
        this.RunAsync(
            [
                "issue",
                "edit",
                id.Key,
                "--repo",
                this._options.Repository,
                labelFlag,
                NeedsHumanLabel,
                assigneeFlag,
                string.Join(ListSeparator, this._options.Maintainers),
            ],
            string.Empty,
            cancellationToken
        );

    private async Task<Fin<string>> RunAsync(
        IReadOnlyList<string> arguments,
        string standardInput,
        CancellationToken cancellationToken
    )
    {
        ProcessRequest request = new(
            this._options.Executable,
            arguments,
            standardInput,
            this._options.WorkingDirectory,
            CommandTimeout
        );
        ProcessOutcome outcome = await this._processes.RunAsync(request, cancellationToken);
        string command = $"{this._options.Executable} {arguments[0]} {arguments[1]}";
        return ToOutput(outcome, command);
    }
}