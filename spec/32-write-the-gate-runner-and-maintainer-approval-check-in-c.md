# Port the gate runner and maintainer approval check to `C#`

## Summary

The gate runner (`scripts/run_gates.py`, `scripts/verify_gates.py`,
`scripts/gates/`) and the "Maintainer approval" check
(`scripts/maintainer_approval.py`) are this repository's own tooling, written
in Python. This item ports both to C#. The gate runner becomes a console
project, `src/AgenticSoftwareWorkflow.Gates`, that does not depend on the
conductor, so a broken conductor change cannot take the gates down. The
approval check becomes a second console project,
`src/AgenticSoftwareWorkflow.MaintainerApproval`. `scripts/gates.sh` shrinks
to a bootstrap that installs the pinned tools and runs the C# runner. The
Python gate set (ruff, pylint with the house-rule checkers, pyright, pytest)
stays, still proven by its canaries, because target projects in Python will
need it. Both C# projects are held to 100% line and branch coverage, so the
coverage exclusion tracked in #9 is removed. The approval check must be at
least as strict afterwards, shown by its tests passing before and after.

## Acceptance criteria

### Gate runner: `run`

- **AC-1**: Given `AgenticSoftwareWorkflow.Gates`, when its project file is
  read, then it has no `ProjectReference` to any other project of the
  solution, and no other project of the solution references it.
- **AC-2**: Given no gate names, when the runner is invoked as `run`, then
  it prints `Running gates in <repository root>` and runs these gates in
  this order: `build`, `format`, `inspect`, `layout`, `ruff`, `pylint`,
  `pyright`, `shellcheck`, `markdownlint`, `lychee`, `actionlint`,
  `snippets`, `test`, `pytest`.
- **AC-3**: Given gate names, when the runner is invoked as `run <names>`,
  then only those gates run, in the order given.
- **AC-4**: Given a gate has run, when it is reported, then the runner
  prints `[PASS] <gate>` or `[FAIL] <gate>` and flushes. For a failed gate
  it then prints the gate's tool output with surrounding whitespace
  trimmed. A passing gate prints no tool output.
- **AC-5**: Given the first selected gate fails, when the runner continues,
  then every remaining selected gate still runs and is reported.
- **AC-6**: Given every selected gate passes, when the runner finishes, then
  its exit code is 0. Given any selected gate fails, then its exit code is
  1.
- **AC-7**: Given a gate result, when it is judged, then it passes only if
  the tool's exit code is 0 and the gate reported no finding.
- **AC-8**: Given `run` with one or more names that are not gates, when it
  starts, then it runs no gate, prints `unknown gate(s): <unknown names,
  sorted, comma separated>; known: <all gate names in order, comma
  separated>`, and exits non-zero.
- **AC-9**: Given a gate's tool is not installed, when the gate runs, then
  the gate fails with an exit code of 127 and output containing
  `<tool>: command not found`. The runner does not crash.
- **AC-10**: Given a gate's tool prints a JSON report, when the tool
  crashes and prints something else, then the gate treats it as a report
  with no findings and still fails through the tool's non-zero exit code.
- **AC-11**: Given the repository target, when the gates run, then the C#
  paths are `src` and `tests`, the Python paths are `scripts` and
  `tests/scripts`, the shell scripts are `scripts/*.sh`, and the Markdown
  and workflow files are those the Python `repository_files` module
  selected. `tests/StyleCanary` is never part of it.
- **AC-12**: Given each Python gate (`ruff`, `pylint`, `pyright`,
  `pytest`), when it runs, then it invokes the same tool, with the same
  arguments, as the Python implementation did (`python -m ruff check
  --output-format json`, `python -m pylint --output-format=json2
  --score=n`, `python -m pyright --outputjson`, `python -m pytest -p
  no:cacheprovider`), and builds its findings from the same JSON fields.
- **AC-13**: Given the `layout` gate, when it runs on C# paths, then it
  reports one finding, with the rule, file name and line number, per
  violation that `scripts/check_line_layout.py` reports, and the gate
  fails when there is any.

### Gate runner: `verify`

- **AC-14**: Given the canary files in `tests/StyleCanary`, when `verify`
  runs, then each `expect: <gate>:<rule>` marker (after `//`, `#` or
  `<!--`) names a finding that the gate must report on that line. A
  finding that is reported on the whole file also satisfies it.
- **AC-15**: Given a marker no gate reported, when `verify` runs, then it
  prints `[MISSED] <gate> did not report <rule> at <file>:<line>` and the
  run fails.
- **AC-16**: Given a static gate that no marker exercises, when `verify`
  runs, then it prints `[UNPROVEN] no canary marker exercises the '<gate>'
  gate` and the run fails.
- **AC-17**: Given the static gates have run on the canary, when `verify`
  summarises them, then it prints `static gates: <reported>/<expected>
  findings reported`.
- **AC-18**: Given `verify`, when it proves the snippets gate, then it
  writes a temporary document `docs/gate-canary-stale-snippet.md` whose
  snippet does not match the tested code, runs the gate, prints `snippets
  gate caught a stale snippet: <True|False>`, and deletes the document even
  if the gate throws. The run fails when the gate passed.
- **AC-19**: Given `verify`, when it proves the C# coverage gate, then it
  writes a temporary uncovered class into
  `src/AgenticSoftwareWorkflow.Conductor`, runs the `test` gate, prints
  `test coverage gate caught an uncovered addition: <True|False>`, and
  deletes the file even if the gate throws. The run fails when the gate
  passed.
- **AC-20**: Given `verify`, when it proves the Python coverage gate, then
  it does the same with a temporary uncovered module in `scripts/lint` and
  the `pytest` gate, printing `pytest coverage gate caught an uncovered
  addition: <True|False>`.
- **AC-21**: Given every proof succeeds, when `verify` finishes, then it
  exits 0. Given any proof fails, then it exits 1, and all proofs still
  ran.
- **AC-22**: Given `tests/StyleCanary` today, when `verify` runs after this
  change, then it catches the same canaries as before: every marker in
  `Violations.cs`, `violations.py`, `violations.sh`, `violations.md` and
  `violations.yml` is reported, and no gate is unproven.

### Bootstrap: `scripts/gates.sh`

- **AC-23**: Given `scripts/gates.sh run [names]`, when it runs, then after
  installing the pinned tools it runs the `Gates` project with `run` and
  the names, and exits with the runner's exit code.
- **AC-24**: Given `scripts/gates.sh verify`, when it runs, then it runs
  the `Gates` project with `verify`, and exits with the runner's exit code.
- **AC-25**: Given any other mode, when `gates.sh` runs, then it prints
  `unknown mode '<mode>': expected run or verify` to standard error and
  exits 2.
- **AC-26**: Given `gates.sh`, when it launches the runner, then it builds
  only the `Gates` project and not the solution, so a compile error in the
  conductor does not stop the runner starting.
- **AC-27**: Given `gates.sh`, when it is inspected, then it still installs
  the pinned Python tools, Node and markdownlint, and lychee with its
  checksum check, puts them on `PATH`, and contains no gate logic. The
  `shellcheck` gate passes on it.
- **AC-28**: Given the repository, when it is searched after this change,
  then none of `scripts/run_gates.py`, `scripts/verify_gates.py`,
  `scripts/gates/` or `scripts/maintainer_approval.py` exists, and the
  Python tests of the deleted modules are gone. `scripts/exit_codes.py`
  is deleted too, unless a remaining Python module still imports it.

### Python gate set stays

- **AC-29**: Given the Python capability, when this change is complete,
  then `scripts/lint/` (the house-rule checkers), their tests under
  `tests/scripts/`, `pyproject.toml`'s ruff, pylint, pyright and pytest
  configuration, `requirements-dev.txt`, and the canaries
  `tests/StyleCanary/violations.py` all remain, and `ruff`, `pylint`,
  `pyright` and `pytest` all pass on the remaining Python.
- **AC-30**: Given `tests/StyleCanary/violations.py`, when `verify` runs,
  then `ruff`, `pylint` and `pyright` each report at least one expected
  finding, as they do now.

### Coverage

- **AC-31**: Given `pyproject.toml`, when it is read, then
  `[tool.coverage.run]` has no `omit` list and no comment pointing to
  issue #9, and the `pytest` gate still fails below 100% line or branch
  coverage.
- **AC-32**: Given the test projects of `Gates` and `MaintainerApproval`,
  when `dotnet test` runs, then each enforces 100% line and branch
  coverage on its project like every other project, and the build fails
  below it.

### Maintainer approval

The behaviour is that of `scripts/maintainer_approval.py`, unchanged. The
command is `AgenticSoftwareWorkflow.MaintainerApproval <owner/repository>
<pull-request-number>`.

- **AC-33**: Given a pull request that changes none of the governed paths,
  when it is judged, then it passes with the reason `No governed paths
  changed.`
- **AC-34**: Given a pull request that changes a path starting with
  `spec/`, `.github/` or `aswf.json`, and no maintainer authored or
  approved it, when it is judged, then it fails with the reason `Changes
  governed paths (<those paths, comma separated>): a maintainer must
  approve commit <head>.` This holds for each of `spec/8.md`,
  `.github/workflows/ci.yml`, `.github/CODEOWNERS` and `aswf.json`.
- **AC-35**: Given a change to `scripts/maintainer_approval.py`, to any
  file under `src/AgenticSoftwareWorkflow.MaintainerApproval/`, or to any
  file under `tests/AgenticSoftwareWorkflow.MaintainerApproval.Test/`,
  when it is judged without approval, then it fails as in AC-34.
- **AC-36**: Given a governed change authored by a maintainer, when it is
  judged, then it passes with the reason `Authored by maintainer
  <author>.`
- **AC-37**: Given a governed change that a maintainer approved at the
  current head commit, when it is judged, then it passes with the reason
  `Approved by <maintainers, comma separated> at <head>.`
- **AC-38**: Given a maintainer's approval of an older commit, when the
  change is judged, then it fails.
- **AC-39**: Given a maintainer who approved the head commit and then
  submitted a later review (for example `CHANGES_REQUESTED`) on it, when
  the change is judged, then it fails: only each reviewer's latest review
  counts.
- **AC-40**: Given an approval of the head commit by someone who is not a
  maintainer, when the change is judged, then it fails.
- **AC-41**: Given a pull request that qualifies under several reasons,
  when it is judged, then the reason reported is the first of: no governed
  paths, authored by a maintainer, approved.
- **AC-42**: Given the command, when it fetches a pull request, then it
  calls `gh` three times, in this order, each with `api` and `--paginate`
  where the list may span pages: `repos/<repository>/pulls/<number>`,
  `repos/<repository>/pulls/<number>/files` and
  `repos/<repository>/pulls/<number>/reviews`. It reads author and head
  commit, filenames, and reviewer, state and commit of each review.
- **AC-43**: Given the maintainers in `aswf.json` at the repository root,
  when the command runs, then those are the maintainers judged against.
- **AC-44**: Given a verdict, when the command finishes, then it prints the
  reason on one line and exits 0 for a pass and 1 for a fail.
- **AC-45**: Given the command is called with other than exactly two
  arguments, or `gh` exits non-zero, or `aswf.json` is unreadable, when it
  runs, then it exits non-zero without passing the pull request (it fails
  closed).
- **AC-46**: Given the Python tests in
  `tests/scripts/test_maintainer_approval.py`, when this change is made,
  then every case in that file has a counterpart among AC-33 to AC-45
  written as a C# test, and the Python file is deleted only in the same
  change that adds them. The same cases pass against the Python
  implementation before the change and the C# implementation after it.
- **AC-47**: Given `.github/workflows/approval.yml`, when it is read, then
  the job is still named `Maintainer approval`, the trigger is still
  `pull_request_target` on `master` for `opened`, `synchronize` and
  `reopened`, permissions are still `contents: read` and `pull-requests:
  read`, and it still checks out only the base branch. It sets up .NET
  from `global.json` instead of Python, and runs the `MaintainerApproval`
  project with `$REPOSITORY` and `$PULL_REQUEST` and `GH_TOKEN`
  unchanged.

### Documentation

- **AC-48**: Given the documents `docs/how-it-works/quality-gates.md`,
  `docs/how-it-works/architecture.md`, `docs/contribute/*.md`,
  `docs/use/getting-started.md`, `README.md`, `CLAUDE.md`,
  `.github/CODEOWNERS` and `tests/StyleCanary/README.md`, when they are
  read, then none refers to a deleted script or module, and none says the
  gate runner or approval check is Python. They state that Python gates
  exist for target projects. Any C# code shown comes from tests through
  `mdsnippets`.
- **AC-49**: Given `scripts/gates.sh verify` and `scripts/gates.sh run`,
  when they run on the finished change, then both exit 0.

## Out of scope

- `scripts/check_line_layout.py` and `scripts/csharp_parens.py`. They stay
  as they are until #3 (the Roslyn analyzer) replaces them; the C# `layout`
  gate calls the former.
- A mechanism for a target project to switch the Python gate set on. The
  gates stay registered and selectable by name, and proven by their
  canaries; a configuration switch is a separate item.
- Progress groups, `--changed-from` and file-based gate selection
  (spec 73). This item ports the runner as it is on `master`; item 73
  builds on the C# runner.
- Changing what any gate checks, adding gates, or changing the pinned tool
  versions.
- Changing who counts as a maintainer or which paths are governed, other
  than adding the new approval project's paths.
- Rewriting the house-rule checkers or their tests in C#.

## Decisions

- **Two projects, not one.** The approval check runs in a different
  workflow, from the base branch, and its source needs governing. Putting
  it in its own project lets only that project's paths be governed, while
  the gate runner stays ungoverned like the rest of `src/`.
- **Project names and places.** `src/AgenticSoftwareWorkflow.Gates` and
  `src/AgenticSoftwareWorkflow.MaintainerApproval`, with tests in
  `tests/AgenticSoftwareWorkflow.Gates.Test` and
  `tests/AgenticSoftwareWorkflow.MaintainerApproval.Test`, following the
  existing naming.
- **The old governed path stays listed.** `scripts/maintainer_approval.py`
  remains governed after deletion, so recreating it also needs approval.
  This only makes the check stricter.
- **Fail closed on bad input.** The Python script crashed (so failed) on
  wrong arguments or a `gh` error. The port makes this explicit, AC-45, so
  the behaviour is tested and not accidental.
- **Settings are found from the repository root.** The Python script used
  a path relative to itself. The C# command uses the nearest ancestor of
  the working directory that contains `aswf.json`, which is the checkout in
  the workflow.
- **The runner is built alone by `gates.sh`.** Otherwise the `Gates`
  project's independence from the conductor would not help when the
  solution fails to compile.
- **The Python gates still run on this repository.** Its remaining Python
  (the house-rule checkers, `check_line_layout.py`) is the Python
  capability's implementation, so keeping those gates running here keeps
  the capability proven and the gate list unchanged.
- **The `layout` gate shells out to `check_line_layout.py`** until #3
  removes it, and takes its findings from the script's printed violations.
- **Port the behaviour on `master`, not spec 73.** Spec 73 changes the same
  runner and is separate; whichever lands second adapts to the first.
- **Prefix matching of governed paths is kept** (`aswf.json` also governs
  `aswf.json.bak`), because changing it would alter the check's strictness.
- **`.github/CODEOWNERS` and the approval workflow change with this item**,
  so the change itself needs a maintainer's approval.
