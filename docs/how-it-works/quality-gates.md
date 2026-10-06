# Quality gates

A gate is a deterministic check with a pass/fail verdict. Gates are defined
once, in [`scripts/gates.py`](../../scripts/gates.py), and run the same way
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
| `ruff-format`, `ruff` | ruff | Python formatting; lint with every rule enabled (naming, complexity ≤ 5, docstrings, …) |
| `pylint` | pylint + [house-rule checkers](../../scripts/lint/house_rules.py) | No conditional expressions, no nested calls, try-aware block nesting, short names, duplicated code |
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
