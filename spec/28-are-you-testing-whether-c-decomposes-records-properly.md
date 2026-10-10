# Remove the record-decomposition tests of expected failures

## Summary

`ExpectedFailureDataTest` constructs each expected-failure record, reads its
properties back, and asserts they equal what was just passed in. Such a test
can fail only if the C# compiler is broken. It most likely exists to cover
property getters that no production code reads, which is coverage padding.
Its test names also disagree with its Arrange/Act/Assert layout: the record
is created in Arrange, while Act is only the decomposition. This item deletes
the file. The behaviour of each failure stays pinned by `ExpectedFailureTest`,
which asserts the `Message` each failure produces. Any property the coverage
gate then flags is either exercised by real behaviour or removed, never
covered by a test that merely reads it back.

## Acceptance criteria

- **AC-1**: Given the repository after the change, when the test project is
  searched, then
  `tests/AgenticSoftwareWorkflow.Conductor.Test/Functional/ExpectedFailureDataTest.cs`
  does not exist and no test class named `ExpectedFailureDataTest` remains.
- **AC-2**: Given the test project after the change, when its tests are
  searched, then none constructs an `ExpectedFailure` subtype only to read
  its constructor-supplied properties back and assert they equal the
  arguments. This covers `AgentTimedOut.Timeout`,
  `AgentProcessFailed.ExitCode` and `StandardError`,
  `AgentOutputMalformed.Detail`, `AgentReportedError.Kind` and `Detail`,
  `CommandFailed.Command`, `ExitCode` and `StandardError`,
  `WorkResponseMalformed.Detail`, `ForeignWorkItem.Id` and `Source`,
  `SpecifierAnswerUnusable.Detail`, and `SettingsUnreadable.Path` and
  `Reason`.
- **AC-3**: Given `ExpectedFailureTest`, when it runs after the change, then
  every existing case of `Message_WhenAFailureOccurs_ExplainsItInASentence`
  and `IsExpected_WhenAFailureOccurs_IsTrue` still passes with unchanged
  expected messages. Each message still includes the data the deleted tests
  covered, for example the exit code and trimmed standard error in
  `AgentProcessFailed`.
- **AC-4**: Given the change is complete, when `dotnet test` runs for the
  test project, then it passes with 100% line and 100% branch coverage and no
  threshold, filter, or exclusion is loosened to achieve that.
- **AC-5**: Given a property of an expected-failure record that no production
  code reads and that the deleted tests alone covered, when the coverage gate
  reports it as uncovered, then it is resolved in one of two ways only. The
  property is removed, because nothing needs it yet. Or a test of real
  behaviour that depends on it is added, such as a message or a caller's
  handling of the failure. No test whose sole purpose is to read the property
  is added.
- **AC-6**: Given any expected-failure type whose data is removed under AC-5,
  when the code is built, then its `Message` text is byte-for-byte what it was
  before, and every other construction site and test still compiles.
- **AC-7**: Given `scripts/gates.sh`, when it runs from WSL after the change,
  then it passes.

## Out of scope

- Writing the future handler (the repair loop deciding whether to retry) or
  tests for it. Failure data that is a contract for a future handler is
  tested when that handler exists.
- Changing any failure's `Message` wording.
- Auditing other test files for the same read-back pattern. A separate item
  can do that.
- Changing coverage thresholds, exclusions, or the coverage standards
  document.

## Decisions

- The whole `ExpectedFailureDataTest` file is deleted rather than renamed or
  reorganised. Every test in it has the same flaw, and the maintainer
  triage chose deletion over re-naming.
- The maintainer's two options are kept, in order of preference. First,
  cover a property through behaviour. Second, remove it. Which applies to
  each property is left to the implementer, because it depends on what the
  coverage gate reports after deletion. AC-5 constrains the outcome.
- Properties that the gate does not flag are left alone. In particular, the
  `[PublicAPI]` attributes on the failure records stay unless a removal under
  AC-5 makes one unnecessary.
