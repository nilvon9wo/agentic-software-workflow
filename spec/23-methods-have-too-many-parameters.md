# Gate: methods may take at most three parameters

## Summary

Methods and explicit constructors with more than three parameters hide an
object that should be extracted and named. This item turns that rule into a
build gate instead of a convention: an established analyzer (SonarAnalyzer's
S107, "methods should not have too many parameters") is added with a maximum
of 3 and reported as an error. A `tests/StyleCanary` violation proves the
gate fires, and the existing offenders are refactored by extracting the
hidden objects, never by passing values through retained state (fields,
properties, or ambient context used as a side channel). Primary-constructor
parameters are dependencies, not call parameters, and are left to issue #24.

## Acceptance criteria

- **AC-1**: Given a method or explicit constructor in a project under `src/`
  or `tests/` (other than `tests/StyleCanary`) declared with four or more
  parameters, when `dotnet build` runs, then the build fails with
  diagnostic S107 reported as an error.
- **AC-2**: Given a method or explicit constructor with exactly three
  parameters, when `dotnet build` runs, then S107 is not reported.
- **AC-3**: Given a class or record whose parameters are declared only in a
  primary constructor (for example `BuildStage(IAgentic agent, IGateKeeping
  gate, IFormatting formatter, GitRepository git)` or
  `ProcessOutcome(int ExitCode, string StandardOutput, string StandardError,
  bool HasTimedOut)`), when `dotnet build` runs, then S107 is not reported
  for it.
- **AC-4**: Given `dotnet_diagnostic.S107.severity` in `.editorconfig`, when
  it is read, then it is `error`, with a comment saying why the limit is 3
  and that primary constructors are exempt. No `none` or `suggestion`
  override of S107 exists for any path.
- **AC-5**: Given the analyzer package is added, when `dotnet build` runs on
  the solution, then no Sonar diagnostic other than S107 is reported or
  enabled (all other Sonar rules are set to `none`, or the package is
  configured to run S107 alone), so this item adds exactly one gate.
- **AC-6**: Given `tests/StyleCanary/Violations.cs`, when it is read, then it
  contains a method with four parameters carrying a
  `// expect: build:S107` marker on the declaration line, and
  `scripts/gates.sh verify` passes, which proves the gate fires. If the
  marker is removed, or S107 stops being enforced, `verify` fails.
- **AC-7**: Given the existing `src/` and `tests/` code, when `dotnet build`
  runs, then S107 reports nothing. In particular the `AgentTask` constructor
  (`AgentRole role, string prompt, string workingDirectory, TimeSpan
  timeout`) and `BuildBrief.AppendFenced` (`StringBuilder brief, string
  heading, string language, string content`) each take at most three
  parameters, by extracting a named type for the values that travel
  together.
- **AC-8**: Given each refactored offender, when the existing tests run,
  then they pass with unchanged observable behaviour (same `AgentTask`
  values, same brief text), and `scripts/gates.sh` passes including 100%
  line and branch coverage.
- **AC-9**: Given the refactored code, when it is read, then no new field,
  property, or static/ambient state exists whose only purpose is to carry a
  value from one method to another in place of a parameter.
- **AC-10**: Given `docs/contribute/coding-standards.md` and
  `docs/how-it-works/quality-gates.md`, when they are read, then they state
  the three-parameter limit, that it is enforced by S107, and that
  primary-constructor parameters are exempt.

## Out of scope

- Primary-constructor parameter counts (tracked in #24).
- Enforcing the ideal of zero or one parameters; only the maximum of three
  is a gate.
- Enabling any other SonarAnalyzer rule.
- Changing the parameter lists of the canary's other deliberately broken
  members.

## Decisions

- The limit is 3, as the maintainer stated: "more than three is evil".
- Only S107 is enabled from SonarAnalyzer (AC-5), to keep the change to the
  one rule asked for. The configuration mechanism (editorconfig or the
  analyzer's own settings file) is left to the implementer, since S107's
  `max` setting may not be read from `.editorconfig`.
- Explicit constructors count as methods; primary constructors of classes and
  records do not, per the maintainer's reply. If S107 cannot be made to skip
  them, the implementer must stop and say so rather than refactor them here.
- The violation for the canary is a build-gate marker (`build:S107`),
  following the existing `build:` markers in `Violations.cs`.
- Which type to extract for each offender is left to the implementer; only
  the resulting parameter counts and unchanged behaviour are specified.
