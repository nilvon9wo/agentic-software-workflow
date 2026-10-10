# Run withheld tests the implementer never sees

## Summary

A build today shows the implementer every test it must pass, so an
implementation can be shaped to the visible tests instead of to the
specification. This item makes the build stage use *withheld tests*. The test
author writes them under `hidden-tests/` beside the visible tests. The
conductor takes `hidden-tests/` out of the workspace before the implementer
runs, so the tests are absent and not merely denied. After the project's code
gate passes, the conductor puts them back and runs them once per criterion,
through a new optional `withheldTestGate` command in `aswf.json`. A failure goes
back to the implementer as the unmet acceptance criterion, copied from the
specification, and never as test code or test output. The withheld tests are
committed with the build, so they stay in the repository as regression guards.

## Acceptance criteria

Terms. *Withheld directory* is `hidden-tests/` at the workspace root
(`WorkspaceLayout.HiddenTests`). *Criterion text* is the whole `AC-n` bullet in
the specification, from its bold label to the end of the bullet.

### Configuration

- **AC-1**: Given `aswf.json` contains `"withheldTestGate": ["bash", "w.sh"]`,
  when `ConductorSettings.Load` reads it, then `WithheldTestGate` is the list
  `["bash", "w.sh"]`.
- **AC-2**: Given `aswf.json` has no `withheldTestGate`, when
  `ConductorSettings.Load` reads it, then loading succeeds and
  `WithheldTestGate` is `null`.
- **AC-3**: Given this repository's own `aswf.json`, when it is loaded, then it
  names a `withheldTestGate` that runs the tests under `hidden-tests/` matching
  a criterion label given as its last argument (`dotnet test` with a filter),
  and `docs/use/getting-started.md` describes the setting next to `codeGate`.

### The test author

- **AC-4**: Given `withheldTestGate` is set, when the build stage briefs the
  test author, then the brief tells it to write withheld tests under
  `hidden-tests/`, to tag each with the one `AC-n` it proves, and to list in
  its answer, as `withheldCriteria`, every label that has at least one
  withheld test.
- **AC-5**: Given `withheldTestGate` is not set, when the build stage briefs
  the test author, then the brief does not mention withheld tests, and the
  build runs exactly as it does today (no withheld step).
- **AC-6**: Given the test author's answer is missing `withheldCriteria`, or
  names a label that is not an `AC-n` bullet in the specification, when the
  stage reads it, then the answer counts as findings for the test author and
  it is asked again, within its existing three attempts. The findings name the
  offending label.
- **AC-7**: Given the test reviewer judges the tests, when it is briefed, then
  the diff it is shown includes the files under `hidden-tests/`, because the
  reviewer must judge them.

### Absent from the implementer

- **AC-8**: Given the tests are approved and `withheldTestGate` is set, when
  the implementer is started (for the first attempt and for every retry), then
  the workspace has no file under `hidden-tests/`, including files that came
  from earlier merged builds.
- **AC-9**: Given the implementer is running, when anything lists the
  conductor's holding place for the withheld files, then that place is outside
  the workspace and outside the repository root's tracked tree, so the
  implementer cannot reach it with the paths its role allows.
- **AC-10**: Given the implementer's workspace, when the code reviewer is shown
  the diff after an implementer attempt, then the diff contains no withheld
  test content.
- **AC-11**: Given the build ends in any way (built, rejected, or failed), when
  the workspace is removed, then the holding place is removed too and no
  withheld file is left outside the workspace.

### Running the withheld tests

- **AC-12**: Given the project's code gate passes, when the stage continues,
  then it restores the withheld files into `hidden-tests/` byte for byte and
  runs `withheldTestGate` once per label in `withheldCriteria`, in label
  order. Each run is in the workspace root, with that label appended as the
  command's last argument, for example `["bash", "w.sh", "AC-3"]`. It does
  this before the code reviewer is asked.
- **AC-13**: Given the code gate fails, when the stage handles the failure,
  then `withheldTestGate` is not run, and the implementer is told the gate's
  report as it is today.
- **AC-14**: Given every withheld run exits 0, when the stage continues, then
  the code reviewer runs as today and the withheld files stay restored in the
  workspace, so that they are committed.
- **AC-15**: Given a withheld run ends, when the stage goes on, then the
  withheld files are taken out of the workspace again before any further
  implementer attempt (AC-8).

### Failure feedback

- **AC-16**: Given the runs for `AC-2` and `AC-5` exit non-zero, when the
  implementer is briefed for its next attempt, then the findings under the
  "What to fix" heading list `AC-2` and `AC-5`, each with its criterion text
  from the specification, in label order.
- **AC-17**: Given a withheld run printed anything (assertion messages, stack
  traces, file paths, test names), when the findings are built, then none of
  that output appears in any brief, in any log line, or in the
  `BuildRejected` message. Only the label and the criterion text appear.
- **AC-18**: Given the findings, when they are read, then they say that
  hidden checks failed for these criteria and do not say how many tests exist
  for them or what the tests do.
- **AC-19**: Given withheld failures on every implementer attempt, when the
  third attempt fails, then the build ends with `BuildRejected` for
  `"implementation"` whose findings are the criterion list of AC-16.
  Withheld failures use the same three attempts as code gate and review
  failures.
- **AC-20**: Given a withheld run times out (the existing 10-minute limit of
  `ProjectCommand`), when the stage handles it, then it counts as that
  criterion being unmet, as in AC-16.

### Merging

- **AC-21**: Given a build finishes with the withheld tests restored, when
  `BuildCommand` commits, then the commit includes the files under
  `hidden-tests/` that the test author wrote, and they reach the pull request.
- **AC-22**: Given a build in which no withheld file was written
  (`withheldCriteria` is empty), when the stage continues after the code gate,
  then `withheldTestGate` is not run and the build continues.
- **AC-23**: Given the pull request description, when it is written, then it
  lists the criteria that have withheld tests, by label only.

### Safety

- **AC-24**: Given `WorkflowRoles`, when the roles are inspected, then
  `Implementer` still denies reads of and edits to `hidden-tests/**`, and
  `TestAuthor` may edit under `hidden-tests/` and may not edit `spec/**` or
  `src/**`. `scripts/live-checks.sh` still shows the implementer unable to read
  a file under `hidden-tests/`.
- **AC-25**: Given the files cannot be moved out of the workspace or back into
  it, when the stage tries, then the build fails with an error that names the
  step and the implementer is never started (for moving out), and no withheld
  file is lost.

## Out of scope

- Running the withheld tests in the project's own CI after the merge. They are
  committed so that the project's checks can run them. Wiring that is the
  project's to do.
- Mutation testing and the arbitrator.
- Withheld tests for the document pipeline (specify).
- Hiding the test author's visible tests, or any change to what the code
  reviewer reads beyond AC-10.
- A summary of withheld failures written by a model. Feedback is deterministic.

## Decisions

- **Hiding by moving.** The test author writes into the one workspace, as
  today. The conductor moves `hidden-tests/` out to a temporary directory
  outside the repository for the implementer's turn, and back afterwards. This
  keeps the test author's tools unchanged and makes the files absent.
- **Earlier builds' hidden tests are hidden too** (AC-8). A workspace comes
  from the base branch, which holds the withheld tests of merged builds, so
  hiding only new files would leak them.
- **One run per criterion.** Feedback must name a criterion without leaking a
  test, and a test runner's output cannot be trusted to do that. Running the
  command once per label gives a pass or fail per criterion from the exit code
  alone. The project's command chooses the tests by label, for example a trait
  filter.
- **`withheldCriteria` in the test author's answer** says which labels to run,
  because most runners exit non-zero when a filter matches nothing.
- **`withheldTestGate` is optional**, like `codeGate`, so existing projects keep
  working. Without it, no withheld tests are requested (AC-5).
- **Order.** The withheld run is after the code gate and before the code
  review, as `architecture.md` step 6 says, so a reviewer never judges code
  that fails a criterion.
- **Attempts.** Withheld failures share the implementer's three attempts rather
  than getting their own budget.
- **Timeouts count as unmet** (AC-20), because the runner cannot tell a slow
  test from a hung one.
