# Stop the loop on systemic failures

## Summary

`RunLoop` currently treats every failure except `UsageLimitReached` as the
item's fault: it logs it and posts it on the item through
`IWorkSupplying.Ask`. When the cause is the environment (agent not signed in,
`gh` unauthenticated, network down), every item in the pass fails the same
way and each gets a `needs-human` comment. This item adds a circuit breaker.
A failure whose type says it cannot be the item's fault is *systemic*. A
systemic failure is not posted on the item, and it ends the pass. When
consecutive passes end systemically, `aswf run` exits with a message and a
non-zero code instead of retrying forever. A stop-file lets a maintainer end
the loop gracefully from another terminal.

## Acceptance criteria

Classification:

- **AC-1**: Given an `AgentProcessFailed`, when its `IsSystemic` is read, then
  it is `true`.
- **AC-2**: Given a `CommandFailed`, when its `IsSystemic` is read, then it
  is `true`.
- **AC-3**: Given any other `ExpectedFailure` (`AgentTimedOut`,
  `AgentOutputMalformed`, `AgentReportedError`, `GatesFailed`,
  `FormattingFailed`, `BuildRejected`, `UsageLimitReached`, and so on), when
  its `IsSystemic` is read, then it is `false`. `ExpectedFailure` declares
  `IsSystemic` as a virtual member defaulting to `false`; the two records
  above override it. `RunLoop` never inspects a failure's message to
  classify it.
- **AC-4**: Given a failure that is not an `ExpectedFailure` (a plain
  LanguageExt `Error`), when `RunLoop` handles it, then it is treated as the
  item's failure (not systemic).

Systemic failure inside a pass:

- **AC-5**: Given a pass with ready items 7 and 8, when processing 7 fails
  with a systemic failure, then `IWorkSupplying.Ask` is not called for 7,
  item 8 is not processed, and the loop logs a line containing the item id
  and the failure's message.
- **AC-6**: Given a systemic failure from `Revise` or `Build` of any item,
  when the loop handles it, then it is handled as in AC-5 (not posted, rest of
  the pass skipped).
- **AC-7**: Given a systemic failure from `UpdateBehindProposals`,
  `ListAwaitingRevision`, `ListReady` or `ListBuildable`, when the pass runs,
  then the loop still logs the existing `Could not …: <message>` line, the
  failure counts as systemic, and the remaining steps of that pass are
  skipped.
- **AC-8**: Given a non-systemic failure such as `AgentTimedOut`, when an item
  fails with it, then behaviour is unchanged: it is logged, posted with
  `Ask`, and the pass continues with the next item.
- **AC-9**: Given an item that hits `UsageLimitReached` and, on the retry
  after the wait, a systemic failure, then the retry is handled as in AC-5.
- **AC-10**: Given a failure of `Ask` itself with a `CommandFailed` while
  escalating a non-systemic failure, then it is only logged, as today; it does
  not count as systemic.

Repeated systemic failure:

- **AC-11**: Given `RunLoopOptions.SystemicFailureLimit` (new, an `int`,
  `2` in `Continuous` and `Once`), when one pass ends systemically and the
  next pass completes without a systemic failure, then the loop keeps
  running and the count of consecutive systemic passes is back to zero.
- **AC-12**: Given a continuous loop, when two consecutive passes each end
  systemically, then `RunLoop.Run` stops without a third pass and returns
  `Fin.Fail` holding a new `ExpectedFailure` named
  `SystemicFailuresRepeated`. Its message states the number of consecutive
  failed passes and the last failure's message, and says the workflow was
  stopped for a problem that is not the items'.
- **AC-13**: Given the first systemic pass in a continuous loop, when it
  ends, then the loop waits `PassInterval` before the next pass, as after any
  pass.
- **AC-14**: Given `RunLoopOptions.Once`, when its single pass ends
  systemically, then `Run` returns the `SystemicFailuresRepeated` failure
  immediately (the pass did not complete).
- **AC-15**: Given `aswf run` or `aswf run --once`, when `RunLoop.Run`
  returns `SystemicFailuresRepeated`, then `CommandLine.Run` writes
  `Failed (SystemicFailuresRepeated): <message>` to the output and returns
  `CommandLine.Failed` (1).
- **AC-16**: Given a pass that ends with no systemic failure, when `Run`
  completes, then it returns `Fin.Succ` and `aswf run --once` prints
  `The pass is complete.` and returns `CommandLine.Succeeded` (0), as today.

Stop-file:

- **AC-17**: Given `RunLoopOptions.StopFile` (new, a file path string) and
  that file exists, when `Run` starts, then no pass is made, no work source
  is queried, and `Run` returns `Fin.Succ` with a message naming the file.
- **AC-18**: Given the stop-file appears while a job is running, when that job
  finishes, then the loop starts no further job in the pass, makes no further
  pass, and returns `Fin.Succ` with a message naming the file.
- **AC-19**: Given the stop-file appears while the loop is pausing between
  passes, when the pause ends, then the loop makes no further pass and
  returns as in AC-18.
- **AC-20**: Given the stop-file does not exist, when the loop runs, then it
  behaves as without this feature.
- **AC-21**: Given the stop-file exists, when the loop stops because of it,
  then the file is not deleted, and the message tells the maintainer to
  remove it before running again.
- **AC-22**: Given `aswf run`, when `CommandLine` builds the options, then
  `StopFile` is `.aswf/stop` under `CommandContext.RepositoryRoot`, and a
  loop stopped by it makes `CommandLine.Run` print the loop's message and
  return `CommandLine.Succeeded` (0).
- **AC-23**: Given a systemic failure and an existing stop-file together,
  when the job fails, then the failure is still not posted on the item and
  the loop stops by the stop-file (AC-18).

Documentation:

- **AC-24**: Given `docs/use/getting-started.md`, when this item is done,
  then it explains the systemic-failure exit and the `.aswf/stop` file.
  Any C# shown comes from tests via `mdsnippets`.

## Out of scope

- Interrupting a pause or a running job when the stop-file appears.
- Making the failure limit or the stop-file path configurable from `aswf.json`
  or the command line.
- Classifying failures by message text or exit code.
- Cleaning up leftover worktrees, or retrying systemic failures within a pass.
- Telling the maintainer by any channel other than the log, the output and the
  exit code.
- Creating the stop-file from a command such as `aswf stop`.

## Decisions

- **Which types are systemic.** Only `AgentProcessFailed` and `CommandFailed`,
  as the item names. A `CommandFailed` raised inside a worker's step (a git
  push, say) is also systemic: the failure type cannot say where it arose,
  and the item asked for type-based classification. A failure of a gate or of
  formatting has its own type (`GatesFailed`, `FormattingFailed`) and stays
  the item's.
- **Mechanism.** A virtual `IsSystemic` on `ExpectedFailure`, overridden by
  the two records, is the polymorphic classification the item asks for.
- **Listing failures count.** The `gh` outage case most often surfaces in the
  list calls, which today are only logged; a systemic one must therefore
  count, or the loop would retry forever.
- **Limit of two.** The item says "say, twice in a row"; I fixed it at 2 in
  `RunLoopOptions` and made it a record member so tests can vary it.
- **`--once` with a systemic failure** returns a failure (exit 1). A
  single pass that could not run is not a success, and "twice in a row" has
  no meaning for one pass.
- **Stop-file checks** happen before the first pass, before each job, and
  after each pause, but do not cut a pause short. The worst-case delay is
  the ten-minute `PassInterval`, which keeps the loop simple. A usage-limit
  wait is not interrupted either; the retry that follows is a job, so the
  check before it applies.
- **Stop-file is not deleted** by the loop, so the maintainer's intent is not
  silently undone; the message tells them to remove it. A stop is a clean
  exit (0), since it is the maintainer's request.
- **Return type.** `RunLoop.Run` returns `Task<Fin<string>>`, so the CLI can
  map the result to a message and exit code with its existing pattern.
