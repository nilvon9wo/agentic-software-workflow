using System.Globalization;
using AgenticSoftwareWorkflow.Conductor.Functional;
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
    private readonly CommandLineTool _gh = new(processes, options.Executable, options.WorkingDirectory);
    private readonly GitHubOptions _options = options;

    private string Source => $"github:{this._options.Repository}";

    public async Task<Fin<IReadOnlyList<WorkItemId>>> ListReady(CancellationToken cancellationToken)
    {
        Fin<string> listed = await this._gh.Run(
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

    public async Task<Fin<WorkItem>> Read(WorkItemId id, CancellationToken cancellationToken)
    {
        Fin<string> viewed = await this.RequireOwn(id).Then(
            _ => this._gh.Run(
                ["issue", "view", id.Key, "--repo", this._options.Repository, "--json", ViewFields],
                string.Empty,
                cancellationToken
            )
        );
        return viewed.Bind(GitHubIssueReader.ReadIssue).Map(this.ToWorkItem);
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

    private static bool IsWaiting(GitHubIssue issue) =>
        (issue.Labels ?? []).Any(label => label.Name == NeedsHumanLabel);

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
}