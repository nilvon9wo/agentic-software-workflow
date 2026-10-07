# Show gate progress and run only the gates a change can affect

## Summary

`scripts/gates.sh run` prints nothing visible in the GitHub UI until it ends,
and it runs every gate on every pull request whatever the pull request
touches. This item makes `scripts/run_gates.py` announce each gate as it
starts, group each gate's output so a passing gate collapses and a failing
one stays visible, and finish with a summary of every gate's verdict. It also
adds a deterministic, file-based selection: given the files a change touches,
only the gates those files can affect run, and `verify` runs only when the
gates themselves may have changed. The mapping fails safe: anything not
recognised runs everything. No AI is involved in selection.

## Acceptance criteria

### Progress and output

- **AC-1**: Given any selection of gates, when `run_gates.py` runs, then
  before each gate starts it prints a line `▶ <gate>` (the gate's name from
  `ALL_GATES`) and flushes it, so the line precedes that gate's verdict line
  in the output.
- **AC-2**: Given `scripts/gates.sh run`, when it launches Python, then it
  does so unbuffered (`python -u`), so output reaches a non-terminal stream
  as it is produced.
- **AC-3**: Given the environment variable `GITHUB_ACTIONS` is `true`, when a
  gate passes, then its `▶ <gate>` line, any tool output and its
  `[PASS] <gate>` line appear between `::group::<gate>` and `::endgroup::`
  lines.
- **AC-4**: Given `GITHUB_ACTIONS` is `true`, when a gate fails, then its
  `::group::<gate>` is closed with `::endgroup::` before the `[FAIL] <gate>`
  line and the tool output are printed, so the failure is not hidden inside
  a collapsed group.
- **AC-5**: Given `GITHUB_ACTIONS` is unset or any value other than `true`,
  when gates run, then no `::group::` or `::endgroup::` line is printed.

### Reporting every gate

- **AC-6**: Given the first selected gate fails, when `run_gates.py` runs,
  then every remaining selected gate still runs and reports, and the exit
  code is 1.
- **AC-7**: Given gates have run, when `run_gates.py` finishes, then its last
  output is a summary containing exactly one line per selected gate, in run
  order, each `[PASS] <gate>` or `[FAIL] <gate>`, and one `[SKIP] <gate>`
  line per gate not selected, so the summary always accounts for every gate
  in `ALL_GATES`.
- **AC-8**: Given every selected gate passes, when `run_gates.py` finishes,
  then the exit code is 0, whether or not other gates were skipped.

### Selecting gates from changed files

The selection is a pure function in a new module `scripts/gates/selection.py`,
`select_for_changes(changed_files: Sequence[str]) -> Selection`. `Selection`
carries the selected gate names (in `ALL_GATES` order) and a boolean
`includes_verify`. Paths are repository-relative with `/` separators.

- **AC-9**: Given changed files ending `.cs`, `.csproj` or `.props`, or named
  `.editorconfig`, `global.json` or `dotnet-tools.json` (at any depth), when
  selecting, then the gates are exactly `build`, `format`, `inspect`,
  `layout`, `test`, `snippets`, and `includes_verify` is false.
- **AC-10**: Given a changed `.py` file outside `scripts/` and
  `tests/StyleCanary/` (for example `tests/scripts/test_x.py`), when
  selecting, then the gates are exactly `ruff`, `pylint`, `pyright`,
  `pytest`, and `includes_verify` is false.
- **AC-11**: Given a changed `.sh` file outside `scripts/`, when selecting,
  then the gates are exactly `shellcheck`.
- **AC-12**: Given a changed `.md` file outside `scripts/`,
  `tests/StyleCanary/` and `.github/`, when selecting, then the gates are
  exactly `markdownlint`, `lychee`, `snippets`, and `includes_verify` is
  false.
- **AC-13**: Given a changed file under `.github/` (including workflow
  `.yml` files), when selecting, then all gates are selected and
  `includes_verify` is true.
- **AC-14**: Given a changed file that is under `scripts/` or
  `tests/StyleCanary/`, or is named `requirements-dev.txt`, `pyproject.toml`
  or `.markdownlint-cli2.jsonc`, when selecting, then all gates are selected
  and `includes_verify` is true, even if the file's own type (for example
  `scripts/gates.sh`, `scripts/gates/x.py`) would otherwise select fewer.
- **AC-15**: Given a changed file whose type is not listed in AC-9 to AC-14
  (for example `aswf.json`, `LICENSE`, `logo.png`), when selecting, then all
  gates are selected and `includes_verify` is true.
- **AC-16**: Given changed files of several kinds (for example one `.cs` and
  one `.md`), when selecting, then the result is the union of each file's
  gates, in `ALL_GATES` order, with no duplicates; and `includes_verify` is
  true if any file's result has it true.
- **AC-17**: Given an empty list of changed files, when selecting, then all
  gates are selected and `includes_verify` is true (fail safe).
- **AC-18**: Given every gate name in `ALL_GATES`, when the mapping is
  examined by a test, then each gate is selected by at least one file type
  in AC-9 to AC-12, so no gate can be silently orphaned by the mapping.

### Command line

- **AC-19**: Given `run_gates.py --changed-from <ref>`, when it runs, then it
  obtains the changed files with `git diff --name-only <ref>...HEAD`, runs
  only the gates `select_for_changes` selects, and reports the rest as
  `[SKIP]` per AC-7.
- **AC-20**: Given `--changed-from <ref>` and the `git diff` command exits
  non-zero (for example an unknown ref), when it runs, then it does not
  fail: it prints a line saying the changes could not be determined and runs
  every gate.
- **AC-21**: Given no `--changed-from`, when `run_gates.py` runs, then every
  gate runs exactly as before, so a local run and a run on `master` are
  always complete.
- **AC-22**: Given `--changed-from <ref>` together with explicit gate names,
  when `run_gates.py` runs, then it exits non-zero with a message that the
  two cannot be combined, and runs no gate.
- **AC-23**: Given `--changed-from` with no following value, when
  `run_gates.py` runs, then it exits non-zero with a usage message.
- **AC-24**: Given `scripts/gates.sh verify --changed-from <ref>`, when the
  selection for the changes has `includes_verify` false, then it prints
  `verify skipped: no change can affect the gates` and exits 0 without
  running the canary.
- **AC-25**: Given `scripts/gates.sh verify --changed-from <ref>`, when
  `includes_verify` is true (including when the changes cannot be
  determined), then the canary runs as for `scripts/gates.sh verify`.
- **AC-26**: Given `scripts/gates.sh verify` with no arguments, when it
  runs, then the canary always runs.

### CI

- **AC-27**: Given a pull request, when `.github/workflows/ci.yml` runs,
  then the checkout has full history (`fetch-depth: 0`) and both gate steps
  pass `--changed-from origin/${{ github.base_ref }}`.
- **AC-28**: Given a push to `master`, when `ci.yml` runs, then neither gate
  step passes `--changed-from`, so every gate and the canary run.

## Out of scope

- Any AI-based decision about which gates to run.
- Parallel execution of gates, or changing the order gates run in.
- Caching gate results between runs.
- Adding annotations or a job summary page beyond the log groups.
- Changing what any individual gate checks.

## Decisions

- **Changed files come from `git diff --name-only <ref>...HEAD`.** That is
  the pull request's own changes against its merge base; uncommitted work is
  not considered because CI runs on a clean checkout.
- **Selection is opt-in via `--changed-from`.** A bare `run` stays complete,
  so the definition of done in `CLAUDE.md` is unchanged for local and AI
  workers; only CI uses the flag, and only for pull requests.
- **Empty or undeterminable change lists run everything.** Fail safe, per
  the item.
- **`verify` skipping is decided inside `gates.sh verify`, not in YAML.**
  The mapping lives in one tested place, and CI needs no extra logic.
- **`::group::` only under `GITHUB_ACTIONS=true`.** Local terminals would
  otherwise show literal workflow commands.
- **A failing gate's output is printed outside its group.** GitHub offers
  no command to expand a group on demand, so closing the group first is how
  the output stays visible.
- **`scripts/**` outranks file type.** A `.md` or `.sh` under `scripts/`
  can change gate behaviour, so it selects everything.
- **Snippets also run for C# changes**, as the item lists, because
  documentation code is generated from test code.
- **`.github/**`, `scripts/**`, `tests/StyleCanary/**` changes** need a
  maintainer's approval under `CLAUDE.md`; this item's implementation
  touches `.github/workflows/ci.yml` and `scripts/`.
