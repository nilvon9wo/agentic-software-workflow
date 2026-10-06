# Coverage Standards

## Line coverage is the floor; branch coverage is the goal

**100% line and branch coverage**, enforced — not aspired to. Every
`dotnet test`, locally and in CI, fails below either threshold: the test
project passes `--coverlet-threshold 100` for both through
`TestingPlatformCommandLineArguments`, with filters in its `testconfig.json`.

- Remove dead code rather than covering it.
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
