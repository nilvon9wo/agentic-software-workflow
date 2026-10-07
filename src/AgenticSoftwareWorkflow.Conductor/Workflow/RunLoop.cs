using AgenticSoftwareWorkflow.Conductor.Agents;
using AgenticSoftwareWorkflow.Conductor.Work;
using LanguageExt;
using LanguageExt.Common;

namespace AgenticSoftwareWorkflow.Conductor.Workflow;

/// <summary>
/// The conductor's outer loop: take every ready item as far as the pipeline
/// goes, then wait and look again. All state lives in the work source, so the
/// loop can be stopped and restarted at any time.
/// </summary>
/// <remarks>
/// A usage limit is waited out, and the same item retried, because nothing is
/// wrong with it. Any other failure is put to a maintainer on the item itself
/// (which stops it being ready), so a broken item never burns usage every pass.
/// </remarks>
public sealed class RunLoop(
    IWorkSupplying work,
    IWorkProcessing processing,
    RunLoopOptions options,
    TextWriter log,
    TimeProvider time
)
{
    private readonly IWorkSupplying _work = work;
    private readonly IWorkProcessing _processing = processing;
    private readonly RunLoopOptions _options = options;
    private readonly TextWriter _log = log;
    private readonly TimeProvider _time = time;

    /// <summary>One pass over the ready items, then (unless running once) another after each pause.</summary>
    public async Task Run(CancellationToken cancellationToken)
    {
        await this.Pass(cancellationToken);
        while (!this._options.RunOnce)
        {
            await this.Pause(this._options.PassInterval, cancellationToken);
            await this.Pass(cancellationToken);
        }
    }

    private static string DescribeFailure(Error failure) =>
        $"""
        The workflow could not take this further: {failure.Message}

        Reply here once it is fixed (or with guidance), and it will try again.
        """;

    // Proposals are kept mergeable first, so nothing waits on a human to click
    // "Update branch". Revisions come next: a maintainer is waiting on them.
    private async Task Pass(CancellationToken cancellationToken)
    {
        Fin<IReadOnlyList<string>> updated = await this._processing.UpdateBehindProposals(cancellationToken);
        await updated.Match(
            Succ: this.LogEach,
            Fail: failure => this.Log($"Could not check the proposals are up to date: {failure.Message}")
        );
        Fin<IReadOnlyList<WorkItemId>> awaitingRevision = await this._processing.ListAwaitingRevision(
            cancellationToken
        );
        await awaitingRevision.Match(
            Succ: ids => this.ProcessEach([.. ids.Select(this.RevisionOf)], cancellationToken),
            Fail: failure => this.Log($"Could not list the proposals awaiting revision: {failure.Message}")
        );
        Fin<IReadOnlyList<WorkItemId>> ready = await this._work.ListReady(cancellationToken);
        await ready.Match(
            Succ: ids => this.ProcessReady(ids, cancellationToken),
            Fail: failure => this.Log($"Could not list the ready items: {failure.Message}")
        );
    }

    private Job RevisionOf(WorkItemId id) => new(id, this._processing.Revise);

    private async Task ProcessReady(IReadOnlyList<WorkItemId> ids, CancellationToken cancellationToken)
    {
        await this.Log($"{ids.Count} item(s) ready.");
        await this.ProcessEach([.. ids.Select(id => new Job(id, this._processing.Run))], cancellationToken);
    }

    private async Task ProcessEach(IReadOnlyList<Job> jobs, CancellationToken cancellationToken)
    {
        foreach (Job job in jobs)
        {
            await this.Process(job, cancellationToken);
        }
    }

    private async Task Process(Job job, CancellationToken cancellationToken)
    {
        Fin<string> report = await job.Step(job.Item, cancellationToken);
        await report.Match(
            Succ: this.Log,
            Fail: failure => this.Handle(job, failure, cancellationToken)
        );
    }

    private Task Handle(Job job, Error failure, CancellationToken cancellationToken) =>
        failure is UsageLimitReached reached
            ? this.WaitOutThenRetry(job, reached, cancellationToken)
            : this.Escalate(job.Item, failure, cancellationToken);

    private async Task WaitOutThenRetry(Job job, UsageLimitReached reached, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this._time.GetUtcNow();
        DateTimeOffset resumeAt = reached.ResetsAt.Match(
            Some: reset => reset + this._options.ResetMargin,
            None: () => now + this._options.UnknownResetWait
        );
        await this.Log($"{reached.Message} Waiting until {resumeAt:u}, then retrying {job.Item}.");
        await this.Pause(resumeAt - now, cancellationToken);
        await this.Process(job, cancellationToken);
    }

    private async Task Escalate(WorkItemId id, Error failure, CancellationToken cancellationToken)
    {
        await this.Log($"{id} failed: {failure.Message}");
        Fin<Unit> asked = await this._work.Ask(id, DescribeFailure(failure), cancellationToken);
        await asked.Match(
            Succ: _ => this.Log($"Asked a maintainer about {id}."),
            Fail: askFailure => this.Log($"Could not ask a maintainer about {id} either: {askFailure.Message}")
        );
    }

    private Task Pause(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration < TimeSpan.Zero ? TimeSpan.Zero : duration, this._time, cancellationToken);

    private async Task LogEach(IReadOnlyList<string> messages)
    {
        foreach (string message in messages)
        {
            await this.Log(message);
        }
    }

    private Task Log(string message) => this._log.WriteLineAsync($"[{this._time.GetUtcNow():u}] {message}");

    /// <summary>One step of the pipeline for one item: specifying it, or revising its proposal.</summary>
    private sealed record Job(WorkItemId Item, Func<WorkItemId, CancellationToken, Task<Fin<string>>> Step);
}