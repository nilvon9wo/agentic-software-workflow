# A usage budget per window, so the loop leaves the maintainer headroom

## Summary

The run loop and the maintainer's own Claude sessions draw on one
subscription, and today the loop stops only when Claude Code reports the plan's
usage limit, so it consumes every window. This item adds an optional budget in
`aswf.json`, `"budget": { "perWindowUsd": 5 }`. The loop totals the
`ListPriceUsd` of the jobs it runs in the current usage window. Once the total
reaches the budget, it starts no further job until the next window, as it
already does for a usage limit. Each pass logs what has been spent, so the
budget can be tuned from real numbers. The window is the five-hour window whose
reset time Claude Code reports in its `rate_limit_event` messages.

## Acceptance criteria

Reporting spend and the window:

- **AC-1**: Given a Claude run whose output has `rate_limit_event` messages
  with `rateLimitType` `five_hour` and `resetsAt` 1791336600, when
  `ClaudeOutputReader` reads it, then the `AgentResult` reports the window
  reset as `DateTimeOffset.FromUnixTimeSeconds(1791336600)`. Only the latest
  `five_hour` event counts, whatever its `status`.
- **AC-2**: Given a Claude run with no `five_hour` event, or one without
  `resetsAt` (other limit types are ignored), when it is read, then the
  `AgentResult` reports no window reset.
- **AC-3**: Given `Run`, `Revise` or `Build` succeeds, when the loop receives
  the result, then the result is a `JobReport(string Summary, decimal
  ListPriceUsd, Option<DateTimeOffset> WindowResetsAt)`. `Summary` is the
  sentence that is logged today. `ListPriceUsd` is the sum over every agent run
  the job made, and `WindowResetsAt` is the latest reset any of those runs
  reported.

Configuration:

- **AC-4**: Given `aswf.json` has no `budget`, when `ConductorSettings.Load`
  reads it, then loading succeeds, no budget applies, and the loop behaves as
  before except for the log line of AC-12.
- **AC-5**: Given `"budget": { "perWindowUsd": 5 }`, when settings load, then
  the loop's budget is 5 US dollars per window.
- **AC-6**: Given `perWindowUsd` is zero, negative, missing, or not a number,
  or `budget` is not an object, when settings load, then loading fails with
  `SettingsUnreadable` naming the file and saying `budget.perWindowUsd` must be
  a positive number.

Spending against the budget:

- **AC-7**: Given a budget of 5 and a window where jobs have reported 3.00 so
  far, when the next job reports 1.50, then the loop's total for that window is
  4.50 and the next job still starts.
- **AC-8**: Given a budget of 5 and a job finishes and brings the window's
  total to 5.00 or more, when another job is due (in the same pass or a later
  one), then the loop does not start it. It logs a line that states the amount
  spent, the budget, and the time it will resume. It then waits until the
  window's reset plus `RunLoopOptions.ResetMargin`, using the same pause as a
  usage limit.
- **AC-9**: Given the wait of AC-8 has ended, when the loop resumes, then the
  total is zero, the job that was held back starts, and no job is skipped or
  run twice.
- **AC-10**: Given a budget and no reported `WindowResetsAt` in the current
  window, when the budget is reached, then the window is taken to end five
  hours after the first spend that was counted in it, and the loop waits until
  then plus `ResetMargin`.
- **AC-11**: Given a budget and the loop is running once (`RunLoopOptions.Once`)
  and the budget is reached mid-pass, when the next job is due, then the loop
  does not wait. It logs which items it left for a later run and the pass ends.
- **AC-12**: Given any pass ends (with or without a budget), when the loop logs
  it, then the log has a line `Spent $X.XX of $Y.YY in this window (resets at
  <UTC time>).` when a budget is set, or `Spent $X.XX in this window.` when
  none is. Amounts are formatted with two decimals using the invariant culture.
  The reset clause is omitted if the window end is not yet known.
- **AC-13**: Given a job fails (including `UsageLimitReached`), when the loop
  handles it, then nothing is added to the total, because a failure carries no
  `JobReport`. A retry after a usage limit is subject to AC-8 like any other
  job.
- **AC-14**: Given no budget is set, when jobs report any amount, then the loop
  never holds a job back because of spend.

Documentation:

- **AC-15**: Given the change, when the docs are read, then
  `docs/use/getting-started.md` documents `budget.perWindowUsd`, the meaning of
  the window, and the log line. Any C# shown comes from tests via `mdsnippets`.

## Out of scope

- A percentage of the plan's allowance: Claude Code does not report one, so
  only dollars are supported.
- Refusing to start a build when less than a build's typical cost remains. It
  is optional in the item, and needs real cost figures from the log of AC-12.
- Reading the maintainer's interactive usage; only the loop's own spend counts.
- Counting the cost of failed jobs, and persisting the total across restarts
  (the total starts at zero when the loop starts).
- Stopping a job part-way when the budget is crossed: the current job always
  finishes.

## Decisions

- The window is the `five_hour` window from Claude Code's `rate_limit_event`
  `resetsAt`. This is the same source the usage-limit wait already uses, and a
  fixed period would drift from the plan's real windows. A fixed five hours from
  the first spend is only the fallback (AC-10).
- There is no default budget (AC-4): any default share would silently throttle
  existing users. Adopting a budget is a deliberate edit to `aswf.json`.
- A reported window reset in the past, or later than the one held, starts a new
  window and zeroes the total. A reset equal to the held one changes nothing.
- A once-run does not wait (AC-11), because `--once` is expected to return.
- The name `JobReport` and its shape (AC-3) are chosen here because the loop
  needs spend from every job type. This changes the `IWorkProcessing` return
  types for `Run`, `Revise` and `Build`.
- Failed jobs add no spend (AC-13): their usage is not available to the loop
  today, and plumbing it through errors is not worth the complexity. The total
  can therefore under-count.
- The budget is checked before each job, so one job may overshoot it by up to
  that job's cost.
