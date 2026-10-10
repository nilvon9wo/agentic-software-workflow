# Arbitrate a build that gives up

## Summary

Today, when `BuildStage` runs out of attempts it fails with `BuildRejected`,
and `RunLoop` posts the last findings on the issue and asks a maintainer to
diagnose them. Often the fault is not in the code: the test reviewer may demand
something the formatter forbids, or the test author may be unable to satisfy a
rule nobody else may fix. This item adds pipeline stage 9 from `architecture.md`.
When a stage runs out of attempts, a read-only arbitrator worker
(`WorkflowRoles.Arbitrator`, strongest tier) reads the specification, the
tests, the implementation, and every round of findings, and decides what is
wrong: the code, the tests, the specification, or the reviewer/workflow. The
conductor then acts on that verdict. The arbitrator changes nothing itself,
never overrides a gate, and runs at most once per call to `BuildStage.Run`, so
it cannot loop.

## Acceptance criteria

Terms. A *round* is one rejection: the findings a reviewer, the formatter, or
the code gate gave a worker, in order, from the start of `BuildStage.Run`. The
*verdict* is the arbitrator's structured answer: a `Fault` of `code`, `tests`,
`specification`, or `workflow`, and a non-empty `Finding` text.

### When the arbitrator runs

- **AC-1**: Given the test author is rejected on every one of its `Attempts`
  (3) goes, whether by the test reviewer or by `FormattingFailed`, when
  `BuildStage.Run` would fail, then it first runs the arbitrator once, instead
  of immediately returning `BuildRejected("tests", …)`.
- **AC-2**: Given the implementer is rejected on every one of its `Attempts`
  (3) goes, whether by `GatesFailed`, `FormattingFailed`, or the code reviewer,
  when `BuildStage.Run` would fail, then it first runs the arbitrator once,
  instead of immediately returning `BuildRejected("implementation", …)`.
- **AC-3**: Given a failure that is not a rejection (the agent failing, a usage
  limit, an unusable worker answer), when it occurs in any build stage, then it
  is returned unchanged and the arbitrator is not run.
- **AC-4**: Given a build in which the arbitrator has already run, when a later
  stage of the same `BuildStage.Run` runs out of attempts, then the arbitrator
  is not run again and the call fails with `BuildRejected` for that stage. The
  arbitrator therefore runs at most once per `BuildStage.Run`.
- **AC-5**: Given a build that passes every stage, when `BuildStage.Run`
  completes, then the arbitrator is not run.

### What the arbitrator is given

- **AC-6**: Given the arbitrator runs, then its `AgentTask` uses
  `WorkflowRoles.Arbitrator`, the job's workspace as working directory, the
  new `RoleInstructions.Arbitrator` loaded from the embedded
  `Workflow/Instructions/arbitrator.md`, and an output schema that requires
  `fault` (an enum of the four values above) and `finding` (a string) and
  allows nothing else.
- **AC-7**: Given the arbitrator runs, then its brief contains the work item as
  `WorkItemBrief` describes it, the approved specification, the diff of the
  workspace as `BuildStage` shows the reviewers, and every round of findings
  from the whole call, in order, each labelled with the stage it came from
  (`tests` or `implementation`) and the attempt number.
- **AC-8**: Given the arbitrator runs, then the workspace is byte-for-byte as
  it was before: the arbitrator role has only read and search tools, and the
  conductor makes no change on its behalf other than those in AC-9 to AC-14.
- **AC-9**: Given the arbitrator's answer has no structured output, is not
  valid for the schema, or has an empty `finding`, when the stage reads it,
  then the build fails with `WorkerAnswerUnusable`, and no other action in
  AC-10 to AC-14 is taken.

### What each verdict does

- **AC-10**: Given `fault` is `code`, when `BuildStage.Run` finishes, then it
  fails with `BuildRejected` for the stage that ran out of attempts, and the
  message of that failure contains the arbitrator's `finding`. The run loop
  then posts it on the issue and sets `needs-human` through the existing
  `RunLoop` escalation, so the rejection stands and its summary is on the
  issue.
- **AC-11**: Given `fault` is `tests`, when `BuildStage.Run` continues, then
  the test author gets exactly one more attempt with the arbitrator's `finding`
  as its findings (under the existing "What to fix" heading), and the result is
  formatted and reviewed by the test reviewer as usual.
- **AC-12**: Given AC-11 and the extra tests attempt is approved, then
  - when the arbitration followed the tests stage, implementation proceeds
    with its normal 3 attempts;
  - when the arbitration followed the implementation stage, the implementer
    starts again with a fresh 3 attempts, and no findings from before the
    tests changed.
- **AC-13**: Given AC-11 and the extra tests attempt is rejected (by the
  formatter or the test reviewer), then the call fails with
  `BuildRejected("tests", …)` carrying that attempt's findings, and the
  arbitrator is not run again (AC-4).
- **AC-14**: Given `fault` is `specification`, when `BuildStage.Run` finishes,
  then it fails with a new expected failure `SpecificationFaulty` (an
  `ExpectedFailure` in `Workflow/Build`) whose `Finding` is the arbitrator's
  `finding`. `BuildCommand.Build` then
  - calls a new `IWorkSupplying.ReturnToSpecifying(WorkItemId, string
    finding)`, which posts the finding as a comment on the issue and moves
    the item from `specified` back to `ready` without `needs-human`;
  - commits, pushes, and proposes nothing, and still removes the workspace;
  - succeeds, with a message that says the item was returned to the
    specifier and gives the finding.
- **AC-15**: Given `fault` is `workflow`, when `BuildStage.Run` finishes, then
  it fails with a new expected failure `WorkflowFaulty` whose `Finding` is the
  arbitrator's `finding`. Its message states that the fault is in the
  reviewer or the workflow rather than the work, and that retrying the item
  will not help until the workflow is fixed, and includes the `finding`. The
  run loop escalates it with `IWorkSupplying.Ask`, so the issue gets the
  evidence and `needs-human`.
- **AC-16**: Given `IWorkSupplying.ReturnToSpecifying` fails, when
  `BuildCommand.Build` handles `SpecificationFaulty`, then the build returns
  that failure, so the run loop escalates it as any other.

### The GitHub adapter and the specifier

- **AC-17**: Given `GitHubIssues.ReturnToSpecifying` on an issue labelled
  `specified`, when it is called, then the finding is posted as a comment and
  the labels become `ready` (without `specified`), using the same label-change
  mechanism as `MarkSpecified` and `MarkBuilt`. If posting the comment
  fails, the labels are not changed.
- **AC-18**: Given an item returned to the specifier whose specification file
  already exists in the workspace, when `SpecifyStage.Run` briefs the
  specifier, then the brief also contains the current specification text under
  a heading "The current specification", and the item's thread includes the
  conductor's finding comment (the workflow's own comments are not filtered
  out by `WorkItemBrief`). When no specification file exists the brief is
  unchanged.
- **AC-19**: Given an item returned to the specifier, when its specification
  is proposed, then it is proposed as an ordinary specification proposal
  needing a maintainer's approval, and `BuildCommand.ListBuildable` excludes
  the item while that proposal is open (existing behaviour).

### Documentation

- **AC-20**: Given `docs/how-it-works/architecture.md`, then its status
  paragraph no longer lists the arbitrator under "Next", and its "Where the
  human comes in" or pipeline section describes the four verdicts and the
  once-per-build limit. Any C# shown comes from a test via `mdsnippets`.

## Out of scope

- Hidden tests and mutation testing, which are separate items.
- Changing `WorkflowRoles.Arbitrator` or any role's access rules, so
  `scripts/live-checks.sh` need not be re-run for this item.
- Arbitrating failures of the specify stage, or failures of the gate that
  are not rejections of a worker's output.
- Letting the arbitrator edit files, run commands, change a gate, or approve
  anything.
- Arbitrating more than once per build, or across separate builds of one
  item. A later build of the same item starts a fresh budget.
- Editing the specification directly. Only a specifier revision can change
  it, approved as usual by a maintainer.
- Automatically fixing the reviewer or workflow when the verdict is
  `workflow`.

## Decisions

- **Which failures reach the arbitrator.** Only the two "ran out of attempts"
  points that now produce `BuildRejected`. Other failures are not about who is
  right, and a usage limit already has its own handling.
- **Budget of one.** "At most once per build attempt" is read as once per
  `BuildStage.Run`. After a `tests` verdict, the extra test-author attempt is
  a single go with no further retries, so there is no loop even when the
  arbitrator is wrong.
- **After a `tests` verdict from the implementation stage,** the implementer
  gets a fresh 3 attempts, because its earlier findings were about tests that
  have since changed.
- **Verdict names and shape.** `code`, `tests`, `specification`, `workflow`
  with one `finding` string, mirroring the four cases in the issue and the
  `ReviewVerdict` pattern (a JSON schema answer through `StructuredAnswers`).
- **`code` and `workflow` reuse the escalation path.** Both end as failures
  that `RunLoop.Escalate` already posts with `IWorkSupplying.Ask`
  (`needs-human`), rather than a new reporting path. `workflow` has its own
  failure type so its message can say the item should not simply be retried.
- **Returning a specification to the specifier** is a new port method,
  `ReturnToSpecifying`, that relabels `specified` to `ready` and posts the
  finding as a comment. The comment comes from the workflow, so it stays in
  the specifier's brief; the specifier is shown the existing specification so
  it can revise rather than write afresh. The existing spec proposal and
  approval flow is reused; no new revision mechanism is added. Whether
  re-specifying reuses the earlier `aswf/specify-<key>` branch is left to
  the existing `GitRepository.CreateWorkspace` behaviour.
- **Arbitrator timeout** of 15 minutes, between the reviewers' 10 and the
  implementer's 30, as it reads more but writes nothing.
