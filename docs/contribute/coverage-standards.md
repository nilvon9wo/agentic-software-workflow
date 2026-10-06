# Coverage Standards

## Line coverage is the floor; branch coverage is the goal

**100% line and branch coverage**, enforced — not aspired to. Every
`dotnet test`, locally and in CI, fails below either threshold: the test
project passes `--coverlet-threshold 100` for both through
`TestingPlatformCommandLineArguments`, with filters in its `testconfig.json`.

- Remove dead code rather than covering it — but first find out *why* it
  looks dead. Code used only from outside the repository is marked, not
  deleted; see the dead-code rule in [coding standards](coding-standards.md#design).
- **Why 100%.** For a library that other projects' *tests* depend on — Xfty
  being the example — a failure must always be traceable to production code
  or to the tests themselves, never to the framework. Untested branches are
  exactly where that guarantee breaks. This project holds the same bar for
  now; any loosening later is a deliberate, documented decision, not drift.
- Coverage proves code *ran*, not that a test would *notice* if it broke.
  Mutation testing (Stryker.NET) closes that gap and is planned as a gate.
- Branch coverage counts both sides of every guard, `switch`, and ternary; a
  test that only exercises the happy path does not satisfy it.

## Errors are loud

A test that fails because of a bug in this project should say so plainly:

- Any misconfiguration fails at the call site with an exception naming the
  problem — `ClaudeInvocation.Headless(" ")` throws `ArgumentException` for
  `model`, rather than producing a command line that fails later, somewhere
  less obvious.
- Accessors that can miss throw rather than return `null`.

## Doc examples are tested code

Every C# block in the documentation is copied by `mdsnippets` from a test that
compiles and passes; CI fails if a document drifts from its source. See
[local development](local-development.md#writing-a-documentation-snippet).
