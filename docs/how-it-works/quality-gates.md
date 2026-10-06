# Quality gates

A gate is a deterministic check with a pass/fail verdict. Gates are defined
once, in [`scripts/gates/`](../../scripts/gates/__init__.py), and run the same way
everywhere — by a developer, by CI, and by every AI worker before its work is
accepted:

```bash
scripts/gates.sh            # every gate against the repository
scripts/gates.sh run build  # just the named gates
scripts/gates.sh verify     # prove every gate still catches its canary
```

A gate fails when its tool exits non-zero **or reports anything at all**.
There is no warning tier: a finding is fixed, or the rule is changed on
purpose with a reason recorded beside it.

## The gates

| Gate | Tool | Catches |
| --- | --- | --- |
| `build` | `dotnet build`, `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors` | Compiler errors and every `.editorconfig` rule the build runs (naming, `var`, `this.`, expression bodies, …) |
| `format` | `dotnet format --verify-no-changes` | Whitespace, line endings, final newline |
| `inspect` | ReSharper CLI `inspectcode` (free) | What Roslyn misses — redundant `using`s, ReSharper naming, and more — down to suggestion level |
| `layout` | [`check_line_layout.py`](../../scripts/check_line_layout.py) | Lines over 120 characters; wrapped calls not closing with `)` on its own line |
| `test` | `dotnet test` + coverlet | Test failures, and line **or** branch coverage below 100% |
| `ruff` | ruff | Python lint with every rule enabled (naming, complexity ≤ 5, docstrings, …) and pycodestyle whitespace rules. There is deliberately no formatter gate: a formatter's layout conflicts with the house layout rules |
| `pylint` | pylint + [house-rule checkers](../../scripts/lint/house_rules.py) | No conditional expressions, no nested calls, try-aware block nesting, layout of boolean chains, wrapped items and comprehensions, short names, duplicated code |
| `pyright` | pyright, strict mode | Python type errors |
| `pytest` | pytest + coverage | Python test failures, and line or branch coverage below 100% |
| `shellcheck` | shellcheck | Shell quoting and portability bugs |

CI also runs `mdsnippets` (every documentation snippet matches the tested code
it was copied from), `markdownlint`, `lychee` (no broken relative links or
anchors), and `actionlint` (GitHub workflow files).

## What testing the gates revealed

These were established by experiment, not taken from documentation. Several
contradict what this project's predecessor assumed:

| Finding | Consequence |
| --- | --- |
| Without `EnforceCodeStyleInBuild`, **no** `.editorconfig` style rule fails the build — even at `severity = error` | The prototype this repo replaced had every rule at `error` and still built with zero diagnostics. Set in `Directory.Build.props`. |
| On .NET 10, the `option = value:error` suffix **is** honoured at build time | It was suspected of being IDE-only; tested, and it is enforced. |
| IDE1006 (naming) **does** run in `dotnet build` | Contrary to some older guidance. |
| IDE0005 (unnecessary `using`) does **not** fail the build | `inspect` catches it instead. |
| `max_line_length` and `csharp_wrap_before_*_rpar` are **never** enforced by Roslyn, and `jb cleanupcode` does not fix them | Hence the `layout` gate. |
| `inspectcode` builds first by default; if the build fails it **writes no report**, which looks exactly like "no findings" | The gate passes `--no-build`; the `build` gate owns compilation. |
| ruff 0.16 changed `format --check` output, which silently broke parsing of its text | The gate reads ruff's JSON output, and tool versions are pinned exactly. |
| coverlet instruments *every* loaded assembly unless told otherwise, so xunit's own DLLs drag the "minimum" coverage to 0% | Filters in the test project's `testconfig.json`. |
| `dotnet format --severity info` fails on `info`-level diagnostics, but the gate's parser only read `error` and `warning` lines | A failure with no finding to explain it — which looked intermittent. The parser reads `info`; a canary-only rule at suggestion level keeps it proven. |

The last three were caught by the canary below before reaching real code.

## Proving the gates work

A gate that silently stops running looks exactly like a gate that passes. So
[`tests/StyleCanary`](../../tests/StyleCanary/README.md) holds deliberately
broken code, each violation tagged with the gate and rule that must catch it:

```csharp
var doubled = amount * 2; // expect: build:IDE0008
```

`scripts/gates.sh verify` runs every gate against the canary and fails if any
tagged violation goes unreported, or if any gate has no canary at all. The two
coverage gates are proven separately: a throwaway uncovered class (C#) or
function (Python) is added to the real code, and the test gate must then fail.

**When you add a rule, add a canary violation for it.** Otherwise nothing
notices when the rule stops being enforced.

## Known gaps

- The `layout` gate is lexical, so it can be fooled by unusual code. It is due
  to be replaced by a Roslyn analyzer, which knows exactly what an invocation is.
- Several house rules (nesting depth, one expression per line, method length)
  are reviewed rather than enforced. Analyzers for them are planned.
- Mutation testing (Stryker.NET) is installed but not yet a gate.

## How changes reach `master`

`master` is protected by a repository ruleset: every change arrives through a
pull request, every required check must pass on a branch that is up to date
with `master`, and force-pushes and deletion are blocked. A pull request with
auto-merge enabled merges itself the moment its checks are green.

The ruleset has **no bypass**, not even for administrators. Workers act through
accounts with write access; a bypass would let one merge a red pull request,
which is exactly what the gates exist to prevent. In an emergency the
maintainer edits the ruleset itself — a deliberate, visible act.

## Maintainer approval

Most changes should merge without a human once the gates pass. A few should
not: a **specification** defines what the tests and code are held to, and
**governance** — the workflows, the approval check itself, and `aswf.json`,
which names the maintainers — defines how the repository runs. A pull request
that changes any of these needs a maintainer.

The required check **Maintainer approval**
([`scripts/maintainer_approval.py`](../../scripts/maintainer_approval.py)) passes
a pull request when:

- it changes no governed path (`spec/`, `.github/`,
  `scripts/maintainer_approval.py`, `aswf.json`); or
- a maintainer authored it; or
- a maintainer's latest review **approves its current head commit**.

### Why a custom check

| Option | Why not |
| --- | --- |
| `CODEOWNERS` + "require review from Code Owners" | Tested: with the ruleset requiring zero approvals, GitHub enforces nothing (`reviewDecision` stays empty). `CODEOWNERS` is kept, but only to *request* the maintainer's review automatically. |
| Require one approval on every pull request | Blocks every worker change on a human, defeating auto-merge; and GitHub forbids approving a pull request authored by your own account. |
| Required reviewers per path in the ruleset | Assigns teams, which exist only in organisations; this repository belongs to a personal account. |

### How it resists tampering

- **The rules come from `master`.** The check runs on `pull_request_target`,
  which takes the workflow and the script from the base branch, so a pull
  request cannot weaken the check that judges it. It never checks out the pull
  request's code, and its token is read-only.
- **Reviews re-run it, but cannot decide it.** `pull_request_target` does not
  fire on reviews, so `approval-on-review.yml` re-runs the trusted check when a
  review is submitted or dismissed. A pull request could tamper with that
  file; the worst it could do is fail to re-run the check, which leaves the
  pull request blocked, not merged.
- **Approval is of a commit.** Pushing new commits after an approval needs a
  new approval; a later "changes requested" by the same maintainer overrides
  their earlier approval.
- **Governance governs itself.** Changing the check, the workflows, or the
  list of maintainers is itself a governed change.

### Risks and limitations

- **Authored by a maintainer counts as approved.** Changes made through the
  maintainer's own account — including those Claude makes on the
  maintainer's behalf in an interactive session — pass without a separate
  review. This is deliberate: GitHub cannot let anyone approve their own pull
  request, and the maintainer directed the change. The workers' bot account
  is never a maintainer.
- **A compromised maintainer account defeats it**, as it would any
  review-based control.
- **Pull requests from forks** cannot re-run the check automatically on review
  (their review event runs with a read-only token); a maintainer re-runs the
  check by hand.
- **Bootstrapping:** a pull request that *adds* this check is not judged by it
  — `pull_request_target` uses the base branch, which does not have it yet. The
  check becomes required only after it is on `master`.
- **The GitHub interface does not say "review required"**; the failing check's
  message says what is needed instead.
- **It trusts GitHub's review record and the `gh` API.** It runs no code of its
  own beyond reading them.
