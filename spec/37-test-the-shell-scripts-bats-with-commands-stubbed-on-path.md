# Test the shell scripts with bats, and hold shell to the C# and Python standards

## Summary

The repository's shell scripts (`scripts/gates.sh`, `scripts/live-checks.sh`)
are only linted by `shellcheck` with its default checks. Their riskiest
branches, such as `gates.sh` rejecting a lychee download whose checksum does
not match, have never run under test. This item holds shell to standards
comparable to C# and Python, in both code and testing. It adds a shell section
to `docs/contribute/coding-standards.md`, mechanical gates for what a tool can
check (`shellcheck` with every optional check, `shfmt` in check-only mode, an
80-column line limit), and a `bats` test gate with a 100% line-coverage
threshold measured by `kcov`. `bats` tests stub external commands (`curl`,
`dotnet`, `sha256sum`, `claude`, and so on) by putting fakes first on `PATH`,
so no test touches anything real. Every new gate is proven by a canary.

## Acceptance criteria

### Tests for the scripts

- **AC-1** Given the repository, when `scripts/gates.sh run bats` runs, then
  every `tests/shell/*.bats` file is executed with `bats-core`, and the gate
  fails if any test fails.
- **AC-2** Given each script `scripts/<name>.sh`, when the tests are listed,
  then `tests/shell/<name>.bats` exists. A script with no matching test file
  makes the `bats` gate fail, naming the script.
- **AC-3** Given any `.bats` test, when it runs, then it runs with `PATH`
  beginning with a per-test stub directory, and with `HOME` and
  `XDG_CACHE_HOME` pointing inside `BATS_TEST_TMPDIR`. No test invokes a real
  `curl`, `dotnet`, `claude`, `sha256sum`, `python3`, or `tar`, and none
  writes outside its temporary directory.
- **AC-4** Given a stubbed command, when a script calls it, then the stub
  appends its name and arguments, one invocation per line, to a call log in
  `BATS_TEST_TMPDIR`, so a test can assert both that a command was called and
  that it was not.
- **AC-5** Given `scripts/gates.sh` is sourced by a test, when sourcing
  finishes, then no function has run. `main "$@"` runs only when the file is
  executed (`"${BASH_SOURCE[0]}" == "${0}"`), not when it is sourced.
- **AC-6** Given each test, when it is named, then its name is
  `<function> when <condition>: <outcome>` (for example
  `ensure_lychee when the checksum differs: exits 1`). There is one test per
  branch of each function, and one behaviour per test. Each test body carries
  the comments `# Arrange`, `# Act`, `# Assert`, and the Act is one statement.

### Behaviour of `scripts/gates.sh` that the tests must pin

- **AC-7** Given `command -v` finds the named command, when `require NAME`
  runs, then it returns 0 and prints nothing.
- **AC-8** Given the named command is absent, when `require NAME` runs, then
  it exits 1 and prints `gates.sh needs 'NAME', which is not installed.` on
  standard error.
- **AC-9** Given the lychee binary is not installed and the download's
  checksum differs from `lychee_sha256`, when `ensure_lychee` runs, then it
  exits 1, prints `lychee 0.24.2 does not match its pinned checksum.` on
  standard error, deletes the downloaded file, never calls `tar`, and creates
  no `lychee` link in the virtual environment.
- **AC-10** Given the lychee binary is not installed and `curl` exits
  non-zero, when `ensure_lychee` runs, then it exits non-zero, never calls
  `sha256sum` or `tar`, and creates no `lychee` link.
- **AC-11** Given the lychee binary is not installed and the checksum
  matches, when `ensure_lychee` runs, then it calls `tar` to extract
  `lychee-x86_64-unknown-linux-musl/lychee` into the versioned lychee
  directory, deletes the downloaded file, and links the binary into the
  virtual environment's `bin`.
- **AC-12** Given the lychee binary is already installed and executable, when
  `ensure_lychee` runs, then it makes no call to `curl`, `sha256sum`, or
  `tar`, and still refreshes the link.
- **AC-13** Given `curl` or `sha256sum` is missing and lychee is not
  installed, when `ensure_lychee` runs, then it fails with the `require`
  message for that command before downloading anything.
- **AC-14** Given the virtual environment's Python is absent, when
  `ensure_python_tools` runs, then it creates the environment with
  `python3 -m venv` and then installs `requirements-dev.txt`. Given it is
  present, then no environment is created and the install still runs.
- **AC-15** Given a private Node of the wrong version or none,
  when `ensure_markdownlint` runs, then it removes the Node directory and
  reinstalls Node with `nodeenv`. Given the right version, then it does not.
- **AC-16** Given the markdownlint stamp file is absent, when
  `ensure_markdownlint` runs, then it runs `npm install` for the pinned
  version and creates the stamp. Given the stamp exists, then it runs no
  `npm`.
- **AC-17** Given mode `run` or no mode, when `main` runs, then it calls
  `python scripts/run_gates.py` with the remaining arguments from the
  repository root. Given mode `verify`, then it calls
  `python scripts/verify_gates.py`.
- **AC-18** Given a mode other than `run` or `verify`, when `main` runs,
  then it returns 2 and prints
  `unknown mode 'MODE': expected run or verify` on standard error.
- **AC-19** Given `python3` or `dotnet` is missing, when `main` runs, then it
  fails with the `require` message before provisioning any tool.
- **AC-20** Given `scripts/live-checks.sh`, when its tests run, then `claude`
  is stubbed, so no test spends subscription usage, and every branch of the
  script is covered by a test named per AC-6.

### Coverage

- **AC-21** Given the `bats` gate runs, when the tests finish, then `kcov`
  has measured line coverage of `scripts/*.sh` only (not the tests or
  helpers), and the gate fails if it is below 100% line coverage, printing
  each uncovered `file:line`.
- **AC-22** Given `kcov` is not installed, when the `bats` gate runs, then it
  fails with a message naming `kcov` and how to install it. It never skips
  coverage silently.
- **AC-23** Given `scripts/gates.sh verify`, when the proof for the `bats`
  gate runs, then it adds a throwaway script containing an untested function,
  confirms the `bats` gate now fails, removes the script, and reports
  `bats coverage gate caught an uncovered addition: True`.
- **AC-24** Given `docs/contribute/coverage-standards.md`, when it is read,
  then it has a shell section stating that coverage is 100% of lines, that
  `kcov` cannot measure Bash branch coverage, that branches are therefore
  covered by one test per branch named per AC-6, and that this limitation is
  deliberate and visible rather than hidden.

### Static gates

- **AC-25** Given `shellcheck` runs, when the gate builds its command, then a
  repository `.shellcheckrc` sets `enable=all` and the severity is `style`, so
  every optional check is on. Findings are reported as `SC<code>`.
- **AC-26** Given the repository's own scripts, `tests/shell/*.bats`, and
  helpers, when the `shellcheck` gate runs, then it reports nothing. Any
  suppression carries a comment on the line above saying why.
- **AC-27** Given a script that uses an unbraced variable (`echo $name`),
  when the canary is verified, then `shellcheck:SC2250` is reported, and
  `violations.sh` carries an `# expect:` marker for it. Each other enabled
  optional check that the repository's scripts rely on gets a marker the same
  way.
- **AC-28** Given the `shfmt` gate, when it runs, then it executes
  `shfmt --diff` (check-only, never writing) with the flags
  `--indent 2 --binary-next-line --case-indent` over every shell script and
  `.bats` helper script, and fails on any difference.
- **AC-29** Given a canary shell file with wrong indentation, when verified,
  then the `shfmt` gate reports it, and the file carries an `# expect:`
  marker.
- **AC-30** Given the `shell-layout` gate, when it runs over `scripts/*.sh`,
  `tests/shell/*.bats`, and shell helpers, then any line over 80 characters
  is reported as rule `line-length` with `file:line`, and the gate passes
  when none exists. A canary line of 81 characters proves it.
- **AC-31** Given `scripts/gates.sh verify`, when it runs, then it fails if
  any of `shfmt`, `shell-layout`, or `bats` has no canary, as it does for
  every other gate.
- **AC-32** Given `docs/how-it-works/quality-gates.md`, when it is read, then
  its gate table lists `bats`, `shfmt`, and `shell-layout`, and the
  `shellcheck` row says it runs with every optional check enabled.

### Tool provisioning

- **AC-33** Given `scripts/gates.sh` is run on a clean machine with `kcov`
  installed, when it provisions tools, then `bats` is installed from npm at a
  pinned exact version into the private Node environment, `shfmt` at a pinned
  exact version from PyPI via `requirements-dev.txt`, and nothing is
  installed globally.
- **AC-34** Given the CI workflow, when it runs, then `kcov` is installed
  before `scripts/gates.sh`, and CI runs nothing else besides it.

### Standards document

- **AC-35** Given `docs/contribute/coding-standards.md`, when it is read,
  then the "Shell" row of the languages table lists the new gates, and a
  "Shell specifics" section states rules for: `snake_case` function and
  variable names; `UPPER_CASE` only for exported variables; `local -r` for
  function-locals and `readonly` for globals that never change; quoting and
  braces on every expansion; 80 columns, hard; functions no longer than
  about 10 lines; blocks nested at most two deep; `set -euo pipefail`; no
  magic values; comments explaining why, never what; and which of these are
  enforced by a gate and which are reviewed by hand.
- **AC-36** Given the shell section, when it states the wrapped-call rule,
  then it matches the C# and Python rule where Bash can, and says where it
  cannot (a `\` continuation cannot put the closing token on its own line).
- **AC-37** Given a shell snippet in the new documentation, when it is
  included, then it comes from a tested file through `mdsnippets`, not typed
  by hand.

## Out of scope

- Branch coverage of Bash. `kcov` does not measure it; the limitation is
  documented (AC-24), not worked around.
- Mechanical enforcement of function length, nesting depth, `local -r`, and
  naming beyond what `shellcheck` and `shfmt` check. These are written into
  the standards and reviewed by hand.
- Making `gates.sh` a thin bootstrap, which is #32. Any logic that #32 moves
  out of `gates.sh` is tested where it lives.
- Rewriting the existing scripts beyond what the new gates and testability
  (AC-5) require.
- Bash mutation testing.
- Changing shell testing for target projects. The capability is built here,
  but only this repository uses it.

## Decisions

- **Order with #32.** #32 has not landed (`gates.sh` still provisions
  lychee, markdownlint, and Python tools). The criteria describe the
  current `gates.sh`. If #32 lands first, AC-9 to AC-16 apply to the same
  behaviour wherever it then lives, and criteria for removed code are
  dropped. The generic criteria (AC-1 to AC-6, AC-21 to AC-37) are
  unaffected.
- **Coverage threshold.** 100% of lines, as the maintainer's decision says,
  because the project's other languages demand 100%. Branches are covered by
  named tests (AC-6).
- **`kcov` is a system prerequisite.** It has no PyPI or npm package, and
  building it needs root-level libraries. Rather than add custom build code,
  `gates.sh` requires it like `dotnet`, and CI installs it from the
  distribution package (AC-22, AC-34). The workflow change needs a
  maintainer's approval.
- **`bats` from npm, `shfmt` from PyPI.** Both are free and established, and
  match how `markdownlint` and `shellcheck` are already provisioned at pinned
  versions in the cache outside the checkout.
- **`shfmt` flags.** Chosen to match the existing layout (two-space indent,
  operators first on a continued line, indented `case` arms). If the
  existing scripts need more than whitespace changes to pass, the flags are
  kept and the scripts are reformatted; the gate is not weakened.
- **Line length.** #31 may provide a line-length gate. This item adds
  `shell-layout` for shell only, 80 columns, because Python is 80 hard and
  the issue asks for it. If #31 has landed and already covers `.sh` and
  `.bats`, the two are merged into the one gate, keeping the name from #31.
- **Test location and naming.** `tests/shell/<script>.bats` mirrors the
  C# convention of one test file per unit. Names read
  `<function> when <condition>: <outcome>` as the closest Bash analogue of
  the C# convention.
- **Testability change.** Guarding `main` so the file can be sourced (AC-5) is
  the standard Bash technique and is the only production change required.
- **`live-checks.sh`.** Its tests stub `claude` (AC-20), because the script
  spends subscription usage when run for real.
