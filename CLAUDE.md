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
- Python, shell, and Markdown are held to equivalent standards (ruff, pyright
  strict, shellcheck, markdownlint).

## Working rules

- Prefer free, established tools over custom code; justify custom code.
- When adding a lint rule or gate, add a violation for it to
  `tests/StyleCanary` so `scripts/gates.sh verify` proves it fires.
- Code in documentation comes from tests via `mdsnippets` — never type C#
  into Markdown by hand.
- Report problems found in Xfty (the test data factory) rather than working
  around them.
- The maintainer is on a Claude Pro plan: work serially, spawn sub-agents only
  when asked, and use the cheapest model that can do a job well.
