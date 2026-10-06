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

    private async Task Pass(CancellationToken cancellationToken)
    {
        Fin<IReadOnlyList<WorkItemId>> ready = await this._work.ListReady(cancellationToken);
        await ready.Match(
            Succ: ids => this.ProcessEach(ids, cancellationToken),
            Fail: failure => this.Log($"Could not list the ready items: {failure.Message}")
        );
    }

    private async Task ProcessEach(IReadOnlyList<WorkItemId> ids, CancellationToken cancellationToken)
    {
        await this.Log($"{ids.Count} item(s) ready.");
        foreach (WorkItemId id in ids)
        {
            await this.Process(id, cancellationToken);
        }
    }

    private async Task Process(WorkItemId id, CancellationToken cancellationToken)
    {
        Fin<string> report = await this._processing.Run(id, cancellationToken);
        await report.Match(
            Succ: this.Log,
            Fail: failure => this.Handle(id, failure, cancellationToken)
        );
    }

    private Task Handle(WorkItemId id, Error failure, CancellationToken cancellationToken) =>
        failure is UsageLimitReached reached
            ? this.WaitOutThenRetry(id, reached, cancellationToken)
            : this.Escalate(id, failure, cancellationToken);

    private async Task WaitOutThenRetry(WorkItemId id, UsageLimitReached reached, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this._time.GetUtcNow();
        DateTimeOffset resumeAt = reached.ResetsAt.Match(
            Some: reset => reset + this._options.ResetMargin,
            None: () => now + this._options.UnknownResetWait
        );
        await this.Log($"{reached.Message} Waiting until {resumeAt:u}, then retrying {id}.");
        await this.Pause(resumeAt - now, cancellationToken);
        await this.Process(id, cancellationToken);
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

    private Task Log(string message) => this._log.WriteLineAsync($"[{this._time.GetUtcNow():u}] {message}");
}