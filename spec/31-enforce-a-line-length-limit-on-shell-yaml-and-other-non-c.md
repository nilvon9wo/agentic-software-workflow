# Enforce a line-length limit on shell and YAML files

## Summary

No gate checks line length for shell scripts or YAML, so lines such as the
112-column ones that #30 wrapped by hand in `scripts/gates.sh` can grow back
unnoticed. This item adds a static gate named `editorconfig`, backed by
[editorconfig-checker](https://github.com/editorconfig-checker/editorconfig-checker)
(installed from PyPI, pinned in `requirements-dev.txt`). It enforces the
`max_line_length` that `.editorconfig` declares for the repository's shell
scripts and GitHub workflow files: 80 columns for `*.sh`, 100 for `*.yml` and
`*.yaml`. A violation of each is added to `tests/StyleCanary` so that
`scripts/gates.sh verify` proves the gate fires.

## Acceptance criteria

- **AC-1**: Given `.editorconfig`, when it is read, then it has a section
  matching `*.sh` that sets `max_line_length = 80`, and a section matching
  `*.yml` and `*.yaml` that sets `max_line_length = 100`. The existing
  `[*]` value of 120 and the `[*.{cs,vb}]` value of 120 are unchanged.
- **AC-2**: Given `requirements-dev.txt`, when it is read, then it lists
  `editorconfig-checker` pinned to an exact version with `==`, like the other
  tools in that file.
- **AC-3**: Given `scripts/gates.sh` and `scripts/run_gates.py`, when
  `scripts/gates.sh run` executes, then a gate named `editorconfig` is among
  the static gates in `STATIC_GATES`, runs, and prints `[PASS] editorconfig`
  or `[FAIL] editorconfig` followed by the tool output on failure.
- **AC-4**: Given a shell file in the `Target`'s `shell_paths` with a line of
  81 or more characters, when the `editorconfig` gate runs, then the gate
  fails and reports a `Finding` whose gate is `editorconfig`, whose rule is
  `max-line-length`, and whose `file_name` and `line_number` are those of the
  offending line.
- **AC-5**: Given a shell file whose longest line is exactly 80 characters,
  when the gate runs, then it reports no finding for that file.
- **AC-6**: Given a YAML file in the `Target`'s `workflow_paths` with a line of
  101 or more characters, when the gate runs, then the gate fails and reports
  a `Finding` with rule `max-line-length` at that file and line.
- **AC-7**: Given a YAML file whose longest line is exactly 100 characters,
  when the gate runs, then it reports no finding for that file.
- **AC-8**: Given the real repository, when `scripts/gates.sh run editorconfig`
  executes, then it passes: every file in `repository_target().shell_paths`
  and `.workflow_paths` already satisfies the limits (the longest current
  line in either is at most 100 characters), and no file is edited to make
  it pass.
- **AC-9**: Given `Target.shell_paths` and `Target.workflow_paths` are both
  empty, when the gate runs, then the tool is not invoked with an empty
  path list that would make it scan the whole repository; the gate returns a
  passing `GateResult` with no findings.
- **AC-10**: Given the gate runs against a file path, when editorconfig-checker
  exits non-zero without any parsable finding (for example it is missing or
  crashes), then the `GateResult` is still failing (non-zero `exit_code`) and
  its `output` contains the tool's output, so a broken tool is never read as
  a pass.
- **AC-11**: Given `tests/StyleCanary/violations.sh`, when it is read, then it
  contains a line longer than 80 characters tagged
  `# expect: editorconfig:max-line-length`, and does not introduce any
  shellcheck finding other than the existing
  `expect: shellcheck:SC2086`.
- **AC-12**: Given `tests/StyleCanary/violations.yml`, when it is read, then
  it contains a line longer than 100 characters tagged
  `# expect: editorconfig:max-line-length`, and it still yields the existing
  `expect: actionlint:expression` finding and no other actionlint finding.
- **AC-13**: Given the canary, when `scripts/gates.sh verify` runs, then the
  `editorconfig` gate reports both canary findings, the output includes no
  `[MISSED]` or `[UNPROVEN]` line for it, and the command exits 0.
- **AC-14**: Given the `editorconfig` gate is removed from `STATIC_GATES` or
  the canary lines are shortened, when `scripts/gates.sh verify` runs, then it
  exits 1 (reporting `[UNPROVEN]` or `[MISSED]` for `editorconfig`).
- **AC-15**: Given the gate is run against the real repository, when
  `tests/StyleCanary` files exist, then they are not checked (the canary is
  only reached through `verify`), exactly as for the other gates.
- **AC-16**: Given the new Python code, when `pytest` runs, then it passes
  with 100% line and branch coverage, and the new code follows the Python
  standards in `docs/contribute/coding-standards.md` (80 columns, no
  conditional expressions, trailing commas).
- **AC-17**: Given `docs/contribute/coding-standards.md` and
  `docs/how-it-works/quality-gates.md`, when read, then each states the 80
  column limit for shell, the 100 column limit for YAML, and names the
  `editorconfig` gate as what enforces them; and all Markdown gates
  (markdownlint, lychee) still pass.

## Out of scope

- Line length of C#, Python and Markdown, which already have the `layout`,
  `ruff` and `markdownlint` gates.
- Running editorconfig-checker on file types other than shell and GitHub
  workflow YAML, and its checks of indentation, final newline and trailing
  whitespace as a goal in themselves (they apply to the files checked, but
  no new rules are configured and existing files are not reformatted).
- Wrapping any existing YAML line to fit 80 columns.
- Changing `scripts/gates.sh` line lengths; they already fit in 80 columns.

## Decisions

- **YAML limit is 100.** The issue left it open. The workflows embed shell
  and `gh`/`jq` expressions whose longest lines are 85 to 100 columns, and
  wrapping them would change `.github/` files (which need maintainer
  approval) and risk breaking quoting. 100 keeps every current file passing
  without edits while still stopping unbounded growth.
- **Scope is `shell_paths` and `workflow_paths` only**, not the whole
  repository, because `.editorconfig`'s `[*]` value of 120 would otherwise
  be applied to Python (held to 80 by `ruff`) and Markdown, duplicating or
  contradicting existing gates.
- **Rule name `max-line-length`** is used for findings and canary markers.
- **Both a shell and a YAML canary** are required, since each limit is a
  separate `.editorconfig` section and either could silently stop applying.
- **The tool is pinned from PyPI** via `requirements-dev.txt`, like
  shellcheck and actionlint, so no installation step is added to
  `scripts/gates.sh`.
