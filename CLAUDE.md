# Agentic Software Workflow — instructions for Claude

This repository is developed by its own workflow. Read
[docs/how-it-works/architecture.md](docs/how-it-works/architecture.md) for how
it works and why.

## Definition of done

`scripts/gates.sh` passes — on Windows, run it from WSL (Smart App Control
intermittently blocks instrumented test DLLs on the Windows side). Never weaken
a gate, threshold, or rule to make work pass; if a rule seems wrong, say so and
stop.

## Standards

Follow [docs/contribute/coding-standards.md](docs/contribute/coding-standards.md)
and [docs/contribute/coverage-standards.md](docs/contribute/coverage-standards.md)
exactly. The rules most often missed:

- `this.` on every instance member; no `var`; explicit types.
- One behaviour per test; AAA comments verbatim; the Act is one statement.
- 100% line **and** branch coverage — every `dotnet test` enforces it.
- Wrapped calls close with `)` on its own line; no line over 120 characters.
- Every suppression carries a comment explaining why.
- Blocks nest at most two deep (a `try` may be a third layer); `catch` only
  specific exception types.
- Python, shell, and Markdown are held to equivalent standards. In Python:
  no conditional expressions (write `if`/`else` with both branches), no call
  nested inside a call inside a call, 100% coverage. There is no formatter:
  lay code out by hand to the layout rules.
- A boolean chain of three or more operands goes one operand per line,
  operator first. Never introduce a name just to break up a line — names
  must communicate intent.
- Do not delete code just because it looks unused — find out why first; mark
  code used from outside with `[PublicAPI]` (other projects) or
  `[UsedImplicitly]` (reflection, frameworks, conventions).
- Python: 80 columns, mandatory trailing commas, and modules split by intent
  rather than by comment banners.

## Working rules

- Prefer free, established tools over custom code; justify custom code.
- When adding a lint rule or gate, add a violation for it to
  `tests/StyleCanary` so `scripts/gates.sh verify` proves it fires.
- Code in documentation comes from tests via `mdsnippets` — never type C#
  into Markdown by hand.
- Roles and their access rules live in `WorkflowRoles`. After changing one,
  run `scripts/live-checks.sh` (spends subscription usage; needs `claude` in
  WSL) — a rule never seen to block anything is not proven.
- Report problems found in Xfty (the test data factory) rather than working
  around them.
- The maintainer is on a Claude Pro plan: work serially, spawn sub-agents only
  when asked, and use the cheapest model that can do a job well.
