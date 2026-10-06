# Vision

The goal of this project: hand an AI one or more documents describing an
application or feature, and get back working, tested, documented software of
consistently high quality — with a human involved only where human judgment is
genuinely required.

This page is the north star. The [architecture](how-it-works/architecture.md)
describes how the project currently pursues it; the
[issue tracker](https://github.com/nilvon9wo/agentic-software-workflow/issues)
tracks the gap between the two.

## 1. Specification

A human provides one or more documents specifying the desired application or
feature.

- Less expensive models handle the ingestion and summarisation of long
  documents.
- An AI identifies missing or ambiguous requirements and resolves them with a
  human before anything is built.

## 2. Tests before code

An AI derives a test suite from the specification: unit, integration,
end-to-end, UI, performance, load, and mutation testing, as the feature
warrants.

- Some tests are shared with the AI that will build the solution; others are
  deliberately **withheld**, so passing them demonstrates the specification was
  met rather than the visible tests gamed.
- Linters check every property of the tests a linter can check.
- A separate AI reviewer checks what a linter cannot:
  - Is the test pyramid sound?
  - Are all branching conditions — or at least all important ones — covered?
  - Are the tests well organised?
  - Does every test clearly communicate its intent?
  - Does every test test exactly one thing, and something useful?
  - Does every assertion assert something useful?
  - When an assertion fails, does its message give enough information to
    diagnose and fix the problem?

## 3. Implementation

The specification and the visible tests — and nothing else — go to a second AI,
which builds the solution. Then, not necessarily in this order:

- Linters run.
- Visible tests run.
- Withheld tests run.
- A separate AI reviewer checks what a linter cannot:
  - Do all classes, methods, and variables name their intentions?
  - Do comments explain *why*, rather than restating (or contradicting) the code?
  - Is high cyclomatic complexity avoided or mitigated?
  - Are there long classes, methods, or lines?
  - Are there deeply nested blocks or expressions?
  - Does the code follow SOLID, YAGNI, DRY, KISS, command-query
    separation, and the principles of *Clean Code*?
  - Is every linter suppression justified? Justified means three things: the
    suppression itself or a comment beside it states the reason; the reason
    is true; and it is a genuinely good reason, not merely a convenient one.

When a check fails once, the work goes back to the implementing AI to fix. When
checks fail **repeatedly**, a different AI determines whether the
specification, the tests, and the solution actually agree — and if not, which
one is wrong, and whether work can safely continue without a human.

When a human must be asked, work that does not depend on the answer continues
in the meantime.

## 4. Documentation

An AI writes documentation covering use, extension, and contribution —
including getting started and a script for manual testers. Tooling guarantees
every example works exactly as documented.

## 5. Human testing and feedback

A human tests manually, both by the prepared script and exploratorily. Defects
go back to the AI for further development, as issues on the GitHub repository
that the workflow monitors.

## Principles

- **Self-hosting.** This project is developed by its own workflow: it is both
  the tool and its first customer.
- **Prior art first.** Free, stable, off-the-shelf tools are used wherever they
  exist; custom code exists only where nothing suitable does.
- **Deterministic where possible.** Anything ordinary software can decide
  reliably is never delegated to a model.
- **Separation of authority.** No agent grades its own work, and no agent can
  quietly change the definition of success.
- **Frugal.** It runs within a Claude Pro subscription: cheap models for cheap
  work, and no paid API required.
