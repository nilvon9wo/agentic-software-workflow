# Hold shell in workflow files to the standard of shell scripts

## Summary

Shell written inline in `.github/workflows/*.yml` `run:` steps is today
checked only indirectly: `actionlint` runs `shellcheck` on it, with several
shellcheck rules switched off, and no canary proves that this happens.
Shell in `scripts/*.sh` gets the full `shellcheck` gate. This item closes
the gap in two ways. First, a canary proves that a shell defect inside a
`run:` step is caught. Second, any `run:` step longer than one line is moved
into a script file under `scripts/`, so it is held to exactly the standard of
any other shell script, and a new gate keeps multi-line shell from creeping
back into workflow files. The two existing multi-line steps (in
`answered.yml` and `approval-on-review.yml`) are extracted.

## Acceptance criteria

- **AC-1.** Given `tests/StyleCanary/violations.yml` contains a `run:` step
  whose script has an unquoted expansion (`echo $1`) tagged
  `# expect: actionlint:shellcheck`, when `scripts/gates.sh verify` runs,
  then the `actionlint` gate reports a finding with rule `shellcheck` on
  that line and `verify` passes. The existing `expression` canary line stays.
- **AC-2.** Given a workflow whose `run:` script spans more than one line
  (a `|` or `>` block scalar with two or more lines, or a quoted scalar
  continued over lines), when `run_inline_shell(target)` runs, then it
  returns a `GateResult` named `inline-shell` with one
  `Finding(gate="inline-shell", rule="multi-line-run", file=<file name>,
  line=<line of the run: key>)` per such step, and a non-zero exit code.
- **AC-3.** Given a workflow whose every `run:` script is one line (for
  example `run: bash scripts/gates.sh run`, or `run: |` with a single
  line), when `run_inline_shell` runs, then it returns no findings and exit
  code 0.
- **AC-4.** Given a workflow with no `run:` steps, or with several steps of
  which only some use `run:`, when `run_inline_shell` runs, then only the
  multi-line `run:` steps are reported, each with its own line.
- **AC-5.** Given a workflow file that is not valid YAML, when
  `run_inline_shell` runs, then it does not raise and reports no findings
  for that file, leaving the error to `actionlint`.
- **AC-6.** Given `tests/StyleCanary/violations.yml` contains a multi-line
  `run:` step tagged `# expect: inline-shell:multi-line-run` on the line of
  its `run:` key, when `scripts/gates.sh verify` runs, then `verify` passes;
  and if the `inline-shell` gate is disabled or made to find nothing,
  `verify` fails.
- **AC-7.** Given the gate registry, when the gates are listed, then
  `inline-shell` is registered directly after `actionlint`, can be selected
  alone with `python scripts/run_gates.py inline-shell`, and is included in
  `scripts/gates.sh run` and `scripts/gates.sh verify`.
- **AC-8.** Given the real repository, when `scripts/gates.sh run` runs,
  then `inline-shell` finds nothing: no file under `.github/workflows/`
  contains a multi-line `run:` script.
- **AC-9.** Given `.github/workflows/answered.yml`, when read, then its
  "Remove the label and assignment if a maintainer replied" step has a
  one-line `run:` of the form `bash scripts/clear_needs_human.sh`, keeps its
  `env:` entries (`GH_TOKEN`, `REPOSITORY`, `ISSUE`, `COMMENTER`), and
  `scripts/clear_needs_human.sh` holds the former script body.
- **AC-10.** Given `scripts/clear_needs_human.sh` run with `COMMENTER` not
  listed (case-insensitively) in `aswf.json`'s `maintainers`, when it runs,
  then it prints `<COMMENTER> is not a maintainer; the issue keeps waiting.`
  to standard output, exits 0, and does not call `gh`.
- **AC-11.** Given `scripts/clear_needs_human.sh` run with a `COMMENTER`
  listed in `maintainers`, when it runs, then it calls
  `gh issue edit "$ISSUE" --repo "$REPOSITORY" --remove-label needs-human
  --remove-assignee <maintainers joined by commas>` exactly once and exits
  with `gh`'s status.
- **AC-12.** Given `.github/workflows/approval-on-review.yml`, when read,
  then its "Re-run the trusted check for this pull request" step has a
  one-line `run:` of the form `bash scripts/rerun_maintainer_approval.sh`,
  keeps its `env:` entries (`GH_TOKEN`, `REPOSITORY`, `PULL_REQUEST`,
  `HEAD_REF`), and `scripts/rerun_maintainer_approval.sh` holds the former
  script body.
- **AC-13.** Given `scripts/rerun_maintainer_approval.sh` run when the
  `approval.yml` run lookup yields an id, when it runs, then it calls
  `gh run rerun <id> --repo "$REPOSITORY"` and exits with `gh`'s status.
- **AC-14.** Given `scripts/rerun_maintainer_approval.sh` run when the
  lookup yields no id, when it runs, then it prints
  `No maintainer-approval run found for pull request <PULL_REQUEST>.` to
  standard error, exits 1, and does not call `gh run rerun`.
- **AC-15.** Given each new script, when read, then it starts with
  `#!/usr/bin/env bash`, has a leading comment saying what it does and which
  environment variables it needs, runs under `set -euo pipefail`, has no
  line over 80 columns (the width of `scripts/gates.sh`), and passes the
  `shellcheck` gate with no suppression.
- **AC-16.** Given `docs/how-it-works/quality-gates.md` and the "Other
  languages" table in `docs/contribute/coding-standards.md`, when read, then
  each lists the `inline-shell` gate and states that workflow `run:` steps
  must be one line, with longer shell kept in `scripts/`.

## Out of scope

- Changing what `answered.yml` and `approval-on-review.yml` do; the
  extraction preserves their behaviour.
- A line-length or layout rule for shell. The standards table lists none for
  shell, and none is added; the 80-column limit in AC-15 is a convention for
  the new files, not a new gate.
- Re-enabling the shellcheck rules `actionlint` switches off for inline
  scripts; extraction makes them moot for the extracted code.
- `.github/workflows/approval.yml` and `ci.yml`, whose `run:` steps are
  already single lines.
- Python or other interpreters in `run:` blocks. Only shell is in question.
- A test framework for shell (such as bats) beyond what AC-10 to AC-14 need.

## Decisions

- The reporter asked whether extracting scripts is worthwhile. Decided yes:
  `actionlint` disables some shellcheck rules for inline scripts, inline
  code cannot be run or tested outside GitHub, and `approval.yml` and
  `ci.yml` already follow the pattern. The overhead is one extra file per
  multi-line step.
- "One line" is the threshold for staying inline, because it is mechanical
  to check and leaves trivial calls such as `bash scripts/gates.sh run`
  inline.
- Enforcement is a new `inline-shell` gate rather than an extension of
  `actionlint`, to keep each gate's purpose single and its findings named for
  the rule broken. It carries a canary, as the project rules require.
- The new scripts are `scripts/clear_needs_human.sh` and
  `scripts/rerun_maintainer_approval.sh`, named for what they do and called
  with `bash scripts/...` as `ci.yml` does, so no executable bit is needed.
- Unparseable YAML is left to `actionlint` (AC-5) so a broken file is
  reported once, by the tool that explains it.
- The existing workflows were not found to violate shellcheck; the issue
  only suspects it, so no separate fix is specified.
- Changes to `.github/` and `spec/` need maintainer approval; this is noted,
  not worked around.
