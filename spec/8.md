# Extend the gates to Markdown and workflow files locally

## Summary

`markdownlint-cli2`, `lychee`, `actionlint` and the documentation-snippet drift
check (`dotnet mdsnippets` must change nothing) run only in CI
(`.github/workflows/ci.yml`, jobs `docs` and the last step of `gates`). A
failure is found only after pushing (as happened in #17). This item adds four
gates to `scripts/gates/` and runs them from `scripts/run_gates.py`, so
`scripts/gates.sh run` is the complete local definition of done. It also extends
`tests/StyleCanary` and `scripts/verify_gates.py` so `scripts/gates.sh verify`
proves each new gate still fires. The new gates are named `markdownlint`,
`lychee`, `actionlint` and `snippets`.

## Acceptance criteria

Gate behaviour (Markdown, links, workflows)

- **AC-1.** Given a `Target` whose new `markdown_paths` field lists Markdown
  files, when `run_markdownlint(target)` runs `markdownlint-cli2` on exactly
  those files with the repository's `.markdownlint-cli2.jsonc`, then it returns
  a `GateResult` named `markdownlint` with one `Finding(gate="markdownlint",
  rule=<rule id, e.g. "MD001">, file_name=<file name>, line_number=<reported
  line>)` per violation reported, and `has_passed` is false.
- **AC-2.** Given Markdown files with no violations, when `run_markdownlint`
  runs, then it returns exit code 0 and no findings, and `has_passed` is true.
- **AC-3.** Given a `Target` whose `markdown_paths` lists Markdown files
  containing a relative link to a missing file, or to an anchor (`#fragment`)
  that does not exist in an existing file, when `run_lychee(target)` runs, then
  it returns a `GateResult` named `lychee` that is not passed and has one
  `Finding(gate="lychee", rule="broken-link", file_name=<the Markdown file>,
  line_number=WHOLE_FILE)` per broken link. The arguments are `--offline
  --include-fragments --no-progress`, identical in effect to the CI step.
- **AC-4.** Given Markdown files whose links all resolve (including an external
  `https://` URL), when `run_lychee` runs, then it passes and makes no network
  access. In offline mode, remote URLs are not checked.
- **AC-5.** Given a `Target` whose new `workflow_paths` field lists workflow
  files, when `run_actionlint(target)` runs `actionlint` on exactly those files,
  then it returns a `GateResult` named `actionlint` with one
  `Finding(gate="actionlint", rule=<actionlint's kind for the error, e.g.
  "expression">, file_name=<file name>, line_number=<reported line>)` per error,
  and is not passed.
- **AC-6.** Given a valid workflow file, when `run_actionlint` runs, then it
  passes with no findings.
- **AC-7.** Given any of these three tools exits non-zero but its output yields
  no parseable findings (for example, the tool is missing or crashes), when the
  gate runs, then the `GateResult` is not passed (exit code propagated) and
  `output` holds the tool's raw output. A gate must never turn a tool failure
  into a pass.

Snippet drift gate

- **AC-8.** Given the repository's Markdown files are all in sync with their
  tested snippets, when `run_snippets(target)` runs `dotnet mdsnippets`, then it
  returns a `GateResult` named `snippets` that passes and no file is modified.
- **AC-9.** Given a Markdown file whose generated snippet block differs from the
  source code, when `run_snippets` runs, then the result is not passed. It has
  one `Finding(gate="snippets", rule="stale-snippet", file_name=<that file>,
  line_number=WHOLE_FILE)` per drifted file, and `output` lists those files.
- **AC-10.** Given drift was detected, when `run_snippets` returns, then every
  Markdown file has exactly the content it had before the gate ran. The gate
  never leaves the working tree modified, so `verify` and `run` are repeatable
  and cannot alter a developer's uncommitted work.
- **AC-11.** Given the developer has uncommitted, uncommitted-but-in-sync, or
  staged changes to Markdown files that do not cause drift, when `run_snippets`
  runs, then it passes. It compares file content before and after `dotnet
  mdsnippets`, not `git diff`, so unrelated edits do not fail it.
- **AC-12.** Given `dotnet mdsnippets` exits non-zero, when `run_snippets` runs,
  then the result is not passed with the tool's output, even if no file changed.

Wiring

- **AC-13.** Given `scripts/run_gates.py` with no arguments, when it runs, then
  all gates run in this order after the existing ones: `markdownlint`, `lychee`,
  `actionlint`, `snippets`, followed by the existing `test` and `pytest` gates.
  Each prints `[PASS]`/`[FAIL] <name>`, and the process exits non-zero if any
  fails. `run_gates.py markdownlint lychee actionlint snippets` selects each by
  name; an unknown name still errors with the list of known gate names, which
  now includes the four new ones.
- **AC-14.** Given the real repository target, then `markdown_paths` is every
  tracked-style `*.md` file in the repo except those matched by the ignores in
  `.markdownlint-cli2.jsonc` and `tests/StyleCanary`, and `workflow_paths` is
  every `*.yml`/`*.yaml` under `.github/workflows`. A violation placed in any
  real doc or workflow fails the matching gate.
- **AC-15.** Given `scripts/gates.sh run` on a machine with only what the script
  already needs (`python3`, `dotnet`) plus whatever runtime the pinned tools
  require, when it starts, then it provisions `markdownlint-cli2`, `lychee` and
  `actionlint` itself at pinned versions equal to those CI now uses, with no
  manual installation step. If a tool's prerequisite is unavailable, it prints a
  message naming the missing tool or prerequisite and exits non-zero. It never
  skips the gate.

Canary proof (`scripts/gates.sh verify`)

- **AC-16.** Given `tests/StyleCanary/violations.md`, when `verify` runs, then
  it contains at least one deliberate `markdownlint` violation and at least one
  deliberate `lychee` broken link (a missing file, and a missing anchor). Each
  is tagged on the offending line with `<!-- expect: <gate>:<rule> -->`, and
  `verify` reports each as found.
- **AC-17.** Given `tests/StyleCanary/violations.yml` (a workflow file outside
  `.github/workflows`), when `verify` runs, then it contains at least one
  deliberate `actionlint` error tagged `# expect: actionlint:<kind>`, and
  `verify` reports it as found.
- **AC-18.** Given `verify_gates.py`'s `EXPECT_MARKER`, when a line carries an
  HTML-comment marker `<!-- expect: gate:rule -->`, then it is recognised
  exactly as `//` and `#` markers are today, and the trailing `-->` (and the
space before it) does not
  become part of the rule name.
- **AC-19.** Given the `markdownlint`, `lychee` or `actionlint` gate is made to
  stop reporting a canary violation, when `verify` runs, then it prints
  `[MISSED] <gate> did not report <rule> at <file>:<line>` and exits non-zero.
  Given any of the three has no canary marker, then `[UNPROVEN] no canary marker
  exercises the '<gate>' gate` is printed and `verify` exits non-zero. Both are
  the existing behaviours, now extended to cover the new gates, so the three new
  static gates must be in `STATIC_GATES`.
- **AC-20.** Given `verify` runs, when it proves the `snippets` gate, then it
  temporarily writes a Markdown file into the real repository containing a stale
  snippet block for an existing snippet. It runs `run_snippets` and requires a
  failure. It deletes the file in a `finally` and prints `snippets gate caught a
  stale snippet: True`. If the gate passes, `verify` exits non-zero.
- **AC-21.** Given the canary files are present, when `run` executes against the
  real repository, then none of `markdownlint`, `lychee`, `actionlint` or
  `snippets` reports anything from `tests/StyleCanary`.
  `.markdownlint-cli2.jsonc` `ignores` and `mdsnippets.json`
  `ExcludeMarkdownDirectories` both include `tests/StyleCanary`.

CI and docs

- **AC-22.** Given `.github/workflows/ci.yml`, when this item is complete, then
  the `gates` job's "Verify documentation snippets" step and the whole `docs`
  job are removed. `bash scripts/gates.sh verify` and `run` are the single
  definition, and they now cover those checks. The workflow still passes
  `actionlint` itself.
- **AC-23.** Given `tests/StyleCanary/README.md`, `CLAUDE.md` and
  `scripts/gates.sh`'s header comment, then they describe the new gates and
  canary files and no longer imply Markdown/workflow/snippet checks are CI-only.
- **AC-24.** Given the Python gate code, then it meets the repo's own gates:
  100% line and branch coverage for new code under `scripts/` via
  `tests/scripts`, and the `ruff`, `pylint`, `pyright` and layout rules, without
  suppressions that weaken a rule.

## Out of scope

- Changing any markdownlint rule, lychee option (it stays offline, with no
  network link checking), actionlint rule, or the mdsnippets configuration
  beyond excluding `tests/StyleCanary`.
- Fixing existing Markdown or workflow violations, apart from any needed to make
  the new gates pass on the real repository.
- Checking external URLs.
- Linting non-Markdown, non-workflow YAML.
- Auto-fixing (`markdownlint --fix`, committing regenerated snippets).
- `scripts/live-checks.sh` and `WorkflowRoles`.

## Decisions

- **Tool provisioning.** The item does not say how to install the three
  non-Python tools, and `requirements-dev.txt` deliberately avoids a system
  Node. I require only that `gates.sh` provisions them automatically at pinned
  versions matching CI (AC-15). The implementer picks the mechanism (for example
  PyPI wheels, `npx` with a pinned version, or a downloaded release binary) and
  justifies it, preferring established tools over custom code. The behaviour is
  the same either way.
- **Snippet gate method.** CI uses `git diff --exit-code`, which would wrongly
  fail on a developer's unrelated uncommitted Markdown edits. I specify a
  before/after content comparison instead (AC-11). The gate restores the files
  on drift (AC-10) so gates never mutate the tree. The cost is that the
  developer must run `dotnet mdsnippets` themselves to fix it. `output` names
  the files and says so.
- **The snippet canary is a temporary file, not a checked-in marker file,**
  because `mdsnippets` would rewrite a checked-in stale canary on every real
  run. The approach matches how the coverage gates are proven today.
- **Finding rule names** for tools that give no rule id: `lychee` uses
  `broken-link` and `snippets` uses `stale-snippet`, both with line
  `WHOLE_FILE`. `actionlint` uses its `kind`.
- **Gate names and order** are `markdownlint`, `lychee`, `actionlint`,
  `snippets`, placed after the existing static gates. `snippets` is kept out of
  `STATIC_GATES` because it is proven by the temporary-file approach, not by
  markers.
- **CI duplication is removed** (AC-22), so there is one definition of done, as
  the item's "complete local definition" implies. Both CI and local runs use the
  same pinned tool versions.
