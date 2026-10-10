using System.Globalization;
using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Processes;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;

namespace AgenticSoftwareWorkflow.Conductor.GitHub;

/// <summary>
/// GitHub Issues as a work source, through the GitHub CLI. An issue labelled
/// <c>ready</c> is ready; <c>needs-human</c>, plus an assignee, marks one
/// waiting on a maintainer; <c>specified</c> replaces <c>ready</c> once its
/// specification has been proposed, and <c>built</c> replaces <c>specified</c>
/// once its implementation has.
/// </summary>
public sealed class GitHubIssues(IProcessCapable processes, GitHubOptions options) : IWorkSupplying
{
    private const string ReadyLabel = "ready";
    private const string NeedsHumanLabel = "needs-human";
    private const string SpecifiedLabel = "specified";
    private const string BuiltLabel = "built";
    private const string UnknownAuthor = "ghost";
    private const string ListFields = "number,labels";
    private const string ViewFields = "number,title,body,labels,comments";
    private const string IssueListLimit = "100";
    private const string ReadFromStandardInput = "-";
    private const string ListSeparator = ",";
    private readonly CommandLineTool _gh = new(processes, options.Executable, options.WorkingDirectory);
    private readonly GitHubOptions _options = options;

    public Task<Fin<IReadOnlyList<WorkItemId>>> ListReady(CancellationToken cancellationToken) =>
        this.ListLabelled(ReadyLabel, cancellationToken);

    public Task<Fin<IReadOnlyList<WorkItemId>>> ListSpecified(CancellationToken cancellationToken) =>
        this.ListLabelled(SpecifiedLabel, cancellationToken);

    public Task<Fin<Unit>> MarkSpecified(WorkItemId id, CancellationToken cancellationToken) =>
        this.Relabel(id, new LabelChange(ReadyLabel, SpecifiedLabel), cancellationToken);

    public Task<Fin<Unit>> MarkBuilt(WorkItemId id, CancellationToken cancellationToken) =>
        this.Relabel(id, new LabelChange(SpecifiedLabel, BuiltLabel), cancellationToken);

    private async Task<Fin<IReadOnlyList<WorkItemId>>> ListLabelled(string label, CancellationToken cancellationToken)
    {
        Fin<string> listed = await this._gh.Run(
            [
                "issue",
                "list",
                "--repo",
                this._options.Repository,
                "--label",
                label,
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

    public async Task<Fin<WorkItem>> Read(WorkItemId id, CancellationToken cancellationToken)
    {
        Fin<string> viewed = await this.RequireOwn(id).Then(
            _ => this._gh.Run(
                ["issue", "view", id.Key, "--repo", this._options.Repository, "--json", ViewFields],
                string.Empty,
                cancellationToken
            )
        );
        Fin<GitHubIssue> issue = viewed.Bind(GitHubIssueReader.ReadIssue);
        Fin<string> workerLogin = await issue.Then(_ => this.WorkerLogin(cancellationToken));
        return issue.Bind(read => workerLogin.Map(login => this.ToWorkItem(read, login)));
    }

    public async Task<Fin<Unit>> Ask(WorkItemId id, string question, CancellationToken cancellationToken)
    {
        Fin<string> commented = await this.RequireOwn(id).Then(
            _ => this._gh.Run(
                ["issue", "comment", id.Key, "--repo", this._options.Repository, "--body-file", ReadFromStandardInput],
                question,
                cancellationToken
            )
        );
        Fin<string> marked = await commented.Then(
            _ => this.Edit(id, "--add-label", "--add-assignee", cancellationToken)
        );
        return marked.Map(_ => Unit.Default);
    }

    public async Task<Fin<Unit>> Resolve(WorkItemId id, CancellationToken cancellationToken)
    {
        Fin<string> unmarked = await this.RequireOwn(id).Then(
            _ => this.Edit(id, "--remove-label", "--remove-assignee", cancellationToken)
        );
        return unmarked.Map(_ => Unit.Default);
    }

    private async Task<Fin<Unit>> Relabel(WorkItemId id, LabelChange change, CancellationToken cancellationToken)
    {
        Fin<string> relabelled = await this.RequireOwn(id).Then(
            _ => this._gh.Run(
                [
                    "issue",
                    "edit",
                    id.Key,
                    "--repo",
                    this._options.Repository,
                    "--remove-label",
                    change.Removed,
                    "--add-label",
                    change.Added,
                ],
                string.Empty,
                cancellationToken
            )
        );
        return relabelled.Map(_ => Unit.Default);
    }

    private static bool IsWaiting(GitHubIssue issue) =>
        (issue.Labels ?? []).Any(label => label.Name == NeedsHumanLabel);

    private IReadOnlyList<WorkItemId> NotWaiting(List<GitHubIssue> issues) =>
        [.. issues.Where(issue => !IsWaiting(issue)).Select(issue => this.IdOf(issue.Number))];

    private WorkItemId IdOf(int number) =>
        new(this._options.Source, number.ToString(CultureInfo.InvariantCulture));

    private Fin<WorkItemId> RequireOwn(WorkItemId id) =>
        id.Source == this._options.Source
            ? Fin.Succ(id)
            : Fin.Fail<WorkItemId>(new ForeignWorkItem(id, this._options.Source));

    private WorkItem ToWorkItem(GitHubIssue issue, string workerLogin) =>
        new(
            this.IdOf(issue.Number),
            issue.Title ?? string.Empty,
            issue.Body ?? string.Empty,
            [.. (issue.Labels ?? []).Select(label => label.Name)],
            [.. (issue.Comments ?? []).Select(comment => this.ToWorkComment(comment, workerLogin))],
            IsWaiting(issue)
        );

    private WorkComment ToWorkComment(GitHubComment comment, string workerLogin)
    {
        string author = comment.Author?.Login ?? UnknownAuthor;
        bool isTrusted = this._options.Maintainers.Contains(author, StringComparer.OrdinalIgnoreCase);
        bool isFromWorkflow = string.Equals(author, workerLogin, StringComparison.OrdinalIgnoreCase);
        return new WorkComment(author, comment.Body ?? string.Empty, isTrusted, isFromWorkflow);
    }

    // Whoever gh is signed in as asked the questions: the workers' account.
    private async Task<Fin<string>> WorkerLogin(CancellationToken cancellationToken)
    {
        Fin<string> printed = await this._gh.Run(["api", "user", "--jq", ".login"], string.Empty, cancellationToken);
        return printed.Map(login => login.Trim());
    }

    private Task<Fin<string>> Edit(
        WorkItemId id,
        string labelFlag,
        string assigneeFlag,
        CancellationToken cancellationToken
    ) =>
        this._gh.Run(
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

    /// <summary>One stage label giving way to the next.</summary>
    private sealed record LabelChange(string Removed, string Added);
}