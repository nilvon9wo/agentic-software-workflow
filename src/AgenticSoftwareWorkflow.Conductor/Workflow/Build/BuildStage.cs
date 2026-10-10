using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Formatting;
using AgenticSoftwareWorkflow.Conductor.Functional;
using AgenticSoftwareWorkflow.Conductor.Gates;
using AgenticSoftwareWorkflow.Conductor.Git;
using LanguageExt;
using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Workflow.Build;

/// <summary>
/// Turns an approved specification into reviewed tests and an implementation
/// that passes the project's checks and a code review, in one workspace.
/// </summary>
/// <remarks>
/// Each role does one job and none grades its own work: the test author writes
/// tests and the test reviewer judges them; the implementer makes them pass,
/// the project's checks and the code reviewer judge that. Findings go back to
/// the worker that must address them, a bounded number of times; after that the
/// build gives up with <see cref="BuildRejected"/> rather than guessing.
/// <para>
/// What a worker writes is formatted by the project's formatter before anyone
/// judges it: layout is a tool's job, so no worker has to remember it and no
/// reviewer has to judge it.
/// </para>
/// </remarks>
public sealed class BuildStage(IAgentic agent, IGateKeeping gate, IFormatting formatter, GitRepository git)
{
    // Each worker gets this many goes at satisfying its reviewer and the checks.
    private const int Attempts = 3;
    private const string NoFindings = "";
    private const string FindingsHeading = "What to fix";
    private const string ChecksFailedHeading = "The project's checks failed after the implementation";

    private readonly IAgentic _agent = agent;
    private readonly IGateKeeping _gate = gate;
    private readonly IFormatting _formatter = formatter;
    private readonly GitRepository _git = git;

    public async Task<Fin<Built>> Run(BuildJob job, CancellationToken cancellationToken)
    {
        Fin<string> tests = await this.WriteTests(new TestAttempt(job, NoFindings, Attempts), cancellationToken);
        return await tests.Then(
            summary => this.Implement(new ImplementAttempt(job, summary, NoFindings, Attempts), cancellationToken)
        );
    }

    private static AgentTask TestAuthorTask(TestAttempt attempt) =>
        Worker.TestAuthor.Task<WorkerReport>(
            attempt.Job,
            BuildBrief.Describe(attempt.Job, FindingsHeading, attempt.Findings)
        );

    private static AgentTask ImplementerTask(ImplementAttempt attempt) =>
        Worker.Implementer.Task<WorkerReport>(
            attempt.Job,
            BuildBrief.Describe(attempt.Job, FindingsHeading, attempt.Findings)
        );

    private static Fin<string> Accepted(Fin<WorkerReport> written) => written.Map(report => report.Text);

    private async Task<Fin<string>> WriteTests(TestAttempt attempt, CancellationToken cancellationToken)
    {
        Fin<WorkerReport> written = await this.Ask<WorkerReport>(TestAuthorTask(attempt), cancellationToken);
        Fin<Unit> formatted = await written.Then(
            _ => this._formatter.Format(attempt.Job.Workspace.Path, cancellationToken)
        );
        return await formatted.Match(
            Succ: _ => this.ReviewTests(attempt, written, cancellationToken),
            Fail: failure => this.RetryTestsIfFixable(attempt, failure, cancellationToken)
        );
    }

    private async Task<Fin<string>> ReviewTests(
        TestAttempt attempt,
        Fin<WorkerReport> written,
        CancellationToken cancellationToken
    )
    {
        Fin<ReviewVerdict> verdict = await this.Review(attempt.Job, Worker.TestReviewer, cancellationToken);
        return await verdict.Then(
            judged => judged.IsApproved
                ? Task.FromResult(Accepted(written))
                : this.RetryTests(attempt, judged.Describe(), cancellationToken)
        );
    }

    // The formatter rejecting the tests is something the test author can fix;
    // any other failure (the agent itself failing, a usage limit) is not, and stops.
    private Task<Fin<string>> RetryTestsIfFixable(
        TestAttempt attempt,
        Error failure,
        CancellationToken cancellationToken
    ) =>
        failure is FormattingFailed
            ? this.RetryTests(attempt, failure.Message, cancellationToken)
            : Task.FromResult(Fin.Fail<string>(failure));

    private Task<Fin<string>> RetryTests(TestAttempt attempt, string findings, CancellationToken cancellationToken) =>
        attempt.AttemptsLeft > 1
            ? this.WriteTests(
                attempt with { Findings = findings, AttemptsLeft = attempt.AttemptsLeft - 1 },
                cancellationToken
            )
            : Task.FromResult(Fin.Fail<string>(new BuildRejected("tests", findings)));

    private async Task<Fin<Built>> Implement(ImplementAttempt attempt, CancellationToken cancellationToken)
    {
        Fin<WorkerReport> report = await this.Ask<WorkerReport>(ImplementerTask(attempt), cancellationToken);
        Fin<Unit> formatted = await report.Then(
            _ => this._formatter.Format(attempt.Job.Workspace.Path, cancellationToken)
        );
        Fin<Unit> checkedByGates = await formatted.Then(
            _ => this._gate.Check(attempt.Job.Workspace.Path, cancellationToken)
        );
        return await checkedByGates.Match(
            Succ: _ => this.ReviewImplementation(attempt, report, cancellationToken),
            Fail: failure => this.RetryIfFixable(attempt, failure, cancellationToken)
        );
    }

    private async Task<Fin<Built>> ReviewImplementation(
        ImplementAttempt attempt,
        Fin<WorkerReport> report,
        CancellationToken cancellationToken
    )
    {
        Fin<ReviewVerdict> verdict = await this.Review(attempt.Job, Worker.CodeReviewer, cancellationToken);
        return await verdict.Then(
            judged => judged.IsApproved
                ? Task.FromResult(Finished(attempt, report))
                : this.Retry(attempt, judged.Describe(), cancellationToken)
        );
    }

    private static Fin<Built> Finished(ImplementAttempt attempt, Fin<WorkerReport> report) =>
        report.Map(done => new Built(attempt.TestsSummary, done.Text));

    // The checks or the formatter rejecting the work is something the implementer
    // can fix; any other failure (the agent itself failing, a usage limit) is not.
    private Task<Fin<Built>> RetryIfFixable(
        ImplementAttempt attempt,
        Error failure,
        CancellationToken cancellationToken
    ) =>
        failure is GatesFailed or FormattingFailed
            ? this.FixTestsThenRetry(attempt, failure.Message, cancellationToken)
            : Task.FromResult(Fin.Fail<Built>(failure));

    // The checks can fail on files only the test author may change: a test's
    // lint, a test project's settings. The implementer may not touch those, so
    // before its next go the test author fixes whatever in the report is its own.
    private async Task<Fin<Built>> FixTestsThenRetry(
        ImplementAttempt attempt,
        string findings,
        CancellationToken cancellationToken
    )
    {
        Fin<WorkerReport> fixedTests = attempt.AttemptsLeft > 1
            ? await this.Ask<WorkerReport>(TestFixerTask(attempt.Job, findings), cancellationToken)
            : Fin.Succ(new WorkerReport(NoFindings));
        return await fixedTests.Then(_ => this.Retry(attempt, findings, cancellationToken));
    }

    private static AgentTask TestFixerTask(BuildJob job, string findings) =>
        Worker.TestAuthor.Task<WorkerReport>(job, BuildBrief.Describe(job, ChecksFailedHeading, findings));

    private Task<Fin<Built>> Retry(ImplementAttempt attempt, string findings, CancellationToken cancellationToken) =>
        attempt.AttemptsLeft > 1
            ? this.Implement(
                attempt with { Findings = findings, AttemptsLeft = attempt.AttemptsLeft - 1 },
                cancellationToken
            )
            : Task.FromResult(Fin.Fail<Built>(new BuildRejected("implementation", findings)));

    private async Task<Fin<ReviewVerdict>> Review(BuildJob job, Worker reviewer, CancellationToken cancellationToken)
    {
        Fin<string> diff = await this._git.Diff(job.Workspace, cancellationToken);
        return await diff.Then(
            changes => this.Ask<ReviewVerdict>(
                reviewer.Task<ReviewVerdict>(job, BuildBrief.ForReview(job, changes)),
                cancellationToken
            )
        );
    }

    private async Task<Fin<T>> Ask<T>(AgentTask task, CancellationToken cancellationToken)
        where T : class, IStructuredAnswer
    {
        Fin<AgentResult> result = await this._agent.Run(task, cancellationToken);
        return result.Bind(StructuredAnswers.Read<T>);
    }

    /// <summary>One go at the tests, and what the previous one was told to fix.</summary>
    private sealed record TestAttempt(BuildJob Job, string Findings, int AttemptsLeft);

    /// <summary>One go at the implementation, and what the previous one was told to fix.</summary>
    private sealed record ImplementAttempt(BuildJob Job, string TestsSummary, string Findings, int AttemptsLeft);

    /// <summary>A role in the build, its standing instructions, and how long it may take.</summary>
    private sealed record Worker(AgentRole Role, string Instructions, TimeSpan Timeout)
    {
        public static Worker TestAuthor { get; } =
            new(WorkflowRoles.TestAuthor, RoleInstructions.TestAuthor, TimeSpan.FromMinutes(20));

        public static Worker TestReviewer { get; } =
            new(WorkflowRoles.TestReviewer, RoleInstructions.TestReviewer, TimeSpan.FromMinutes(10));

        public static Worker Implementer { get; } =
            new(WorkflowRoles.Implementer, RoleInstructions.Implementer, TimeSpan.FromMinutes(30));

        public static Worker CodeReviewer { get; } =
            new(WorkflowRoles.CodeReviewer, RoleInstructions.CodeReviewer, TimeSpan.FromMinutes(10));

        /// <summary>This worker's task in the job's workspace, answering as <typeparamref name="T"/>.</summary>
        public AgentTask Task<T>(BuildJob job, string brief)
            where T : IStructuredAnswer =>
            new AgentTask(this.Role, brief, job.Workspace.Path, this.Timeout)
                .WithInstructions(this.Instructions)
                .WithOutputSchema(T.Schema);
    }
}