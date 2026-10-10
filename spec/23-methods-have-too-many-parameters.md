# Gate: methods may take at most three parameters

## Summary

Methods and explicit constructors with more than three parameters hide an
object that should be extracted and named. This item turns that rule into a
build gate instead of a convention: an established analyzer (SonarAnalyzer's
S107, "methods should not have too many parameters") is added with a maximum
of 3 and reported as an error. The one exception is a
`System.Threading.CancellationToken`, which is allowed as an additional
fourth parameter and does not count toward the limit. A `tests/StyleCanary`
violation proves the gate fires, and the existing offenders are refactored by
extracting the hidden objects, never by passing values through retained state
(fields, properties, or ambient context used as a side channel).
Primary-constructor parameters are dependencies, not call parameters, and are
left to issue #24.

## Acceptance criteria

- **AC-1**: Given a method or explicit constructor in a project under `src/`
  or `tests/` (other than `tests/StyleCanary`) declared with four or more
  parameters that are not `CancellationToken`, when `dotnet build` runs, then
  the build fails with diagnostic S107 reported as an error.
- **AC-2**: Given a method or explicit constructor with exactly three
  parameters, when `dotnet build` runs, then S107 is not reported.
- **AC-3**: Given a class or record whose parameters are declared only in a
  primary constructor (for example `BuildStage(IAgentic agent, IGateKeeping
  gate, IFormatting formatter, GitRepository git)` or
  `ProcessOutcome(int ExitCode, string StandardOutput, string StandardError,
  bool HasTimedOut)`), when `dotnet build` runs, then S107 is not reported
  for it.
- **AC-4**: Given `dotnet_diagnostic.S107.severity` in `.editorconfig`, when
  it is read, then it is `error`, with a comment saying why the limit is 3,
  that primary constructors are exempt, and that a `CancellationToken` is
  exempt. No `none` or `suggestion` override of S107 exists for any path.
- **AC-5**: Given the analyzer package is added, when `dotnet build` runs on
  the solution, then no Sonar diagnostic other than S107 is reported or
  enabled (all other Sonar rules are set to `none`, or the package is
  configured to run S107 alone), so this item adds exactly one gate.
- **AC-6**: Given `tests/StyleCanary/Violations.cs`, when it is read, then it
  contains a method with four parameters, none of them a `CancellationToken`,
  carrying a `// expect: build:S107` marker on the declaration line, and
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
  the three-parameter limit, that it is enforced by S107, that
  primary-constructor parameters are exempt, and that a `CancellationToken`
  is allowed as an additional fourth parameter.
- **AC-11**: Given a method or explicit constructor with three parameters that
  are not `CancellationToken` plus one `CancellationToken` (in any position),
  when `dotnet build` runs, then S107 is not reported for it.
- **AC-12**: Given a method or explicit constructor with four parameters that
  are not `CancellationToken` plus one `CancellationToken`, when
  `dotnet build` runs, then S107 is reported as an error.
- **AC-13**: Given a method with two `CancellationToken` parameters and three
  others, when `dotnet build` runs, then S107 is reported as an error, because
  only one token is exempt.
- **AC-14**: Given the exemption is in place, when `tests/StyleCanary` is
  built by `scripts/gates.sh verify`, then it contains a method with three
  non-token parameters plus a `CancellationToken` and no `expect` marker, and
  `verify` passes, which proves the exemption holds. The exemption is
  general; it is not achieved by suppressing S107 on individual methods.

## Out of scope

- Primary-constructor parameter counts (tracked in #24).
- Enforcing the ideal of zero or one parameters; only the maximum of three
  is a gate.
- Enabling any other SonarAnalyzer rule.
- Changing the parameter lists of the canary's other deliberately broken
  members.
- A comparable cancellation-parameter exception for other languages. The
  maintainer allows one if such a parameter exists there; Python has no
  standard cancellation token and this item adds no parameter-count gate
  for Python.

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
- Review change: a `CancellationToken` is exempt from the count, as the
  maintainer said (AC-1, AC-4, AC-10, AC-11, AC-12, AC-14). The summary and
  out of scope were updated to match.
- Review change: "fourth parameter" is read as: at most three other
  parameters plus one token. Two tokens do not both qualify (AC-13); this is
  the narrowest reading of "a fourth parameter".
- Review change: the token may be in any position, since the review gave no
  position and the .NET convention (last) is not worth enforcing here
  (AC-11).
- Review change: S107 has no built-in exemption for a parameter type, so the
  implementer chooses how to realise it (for example a custom or additional
  established analyzer rule). The outcome is fixed by AC-11 to AC-14: S107 is
  not reported for the exempt case, and the exemption is not a per-method
  suppression.
- Review change: no other-language exception is specified. The review says
  one may exist where a comparable token exists; none applies to this
  repository's Python today, and no Python parameter gate is in this item.
