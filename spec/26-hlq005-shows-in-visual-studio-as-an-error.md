# Hyperlinq analyzer as a gate; HLQ005 off in tests

## Summary

Visual Studio reports `HLQ005` ("Single() and SingleOrDefault() ... Use
First() or FirstOrDefault() instead") as an error in test projects. The
maintainer decided that the Hyperlinq analyzer becomes a real gate, that every
diagnostic it reports is fixed and never ignored, and that `HLQ005` is switched
off for test projects, because in tests `Single()` is the assertion that there
is exactly one element. An earlier attempt showed that
`NetFabric.Hyperlinq.Analyzer` 2.3.0 (the latest on NuGet) runs in the build
but never reports `HLQ005` for `Single()`; the Visual Studio extension does.
So the gate must be proven with a Hyperlinq rule that the package really
reports in a build, not with `HLQ005`. This item adds the analyzer as a pinned
package, fixes what it reports, turns `HLQ005` off for tests with the reason
recorded, and adds a canary for a rule that fires, so `scripts/gates.sh verify`
proves the gate works.

## Acceptance criteria

- **AC-1** Given `Directory.Packages.props`, when it is read, then it contains
  a `PackageVersion` entry for `NetFabric.Hyperlinq.Analyzer` with the explicit
  fixed version `2.3.0` (no range or wildcard), alongside the existing pinned
  packages.
- **AC-2** Given `Directory.Build.props`, when it is read, then it contains a
  `PackageReference` to `NetFabric.Hyperlinq.Analyzer` with
  `PrivateAssets="all"`, in the same item group as the existing
  `Microsoft.VisualStudio.Threading.Analyzers` reference, with no `Version`
  attribute. The analyzer therefore runs in every project's build, including
  projects added later.
- **AC-3** Given a clean checkout, when `dotnet build` runs on the solution,
  then it succeeds with no Hyperlinq (`HLQ`) diagnostics, errors or warnings:
  every finding in any project has been fixed in code, except `HLQ005` in
  tests (AC-4). No other `HLQ` rule is disabled anywhere, and no `HLQ` finding
  is suppressed.
- **AC-4** Given `.editorconfig`, when it is read, then the existing
  `[tests/**.cs]` section sets `dotnet_diagnostic.HLQ005.severity = none`, with
  a comment above it stating that in tests `Single()` is the assertion that
  there is exactly one element, so `First()` would weaken it.
- **AC-5** Given the test projects that call `Single()` (for example
  `ClaudeCodeAgentRunnerTest.cs`, `ClaudeOutputReaderTest.cs` and
  `CommandLineTest.cs` in `tests/AgenticSoftwareWorkflow.Conductor.Test`), when
  they are built, then no `HLQ005` diagnostic is reported, and those
  `Single()` calls are unchanged.
- **AC-6** Given `tests/StyleCanary/Violations.cs`, when it is read, then it
  contains a line that violates a Hyperlinq rule `HLQnnn` that
  `NetFabric.Hyperlinq.Analyzer` 2.3.0 is observed to report in a build, and
  the line carries the marker `// expect: build:HLQnnn` in the format of the
  existing markers. `HLQ005` is not used for the canary.
- **AC-7** Given the canary line from AC-6, when `scripts/gates.sh verify`
  runs, then it passes, with the build reporting `HLQnnn` on the marked line.
  If `tests/StyleCanary/.editorconfig` or the `[tests/**.cs]` section would
  silence or downgrade `HLQnnn`, the canary's `.editorconfig` sets
  `dotnet_diagnostic.HLQnnn.severity = error` with a comment explaining why.
- **AC-8** Given the canary line, when the package reference from AC-2 is
  removed, then `scripts/gates.sh verify` fails with
  `[MISSED] build did not report HLQnnn` for the marked line.
- **AC-9** Given the changes, when `scripts/gates.sh` runs (from WSL), then it
  passes in full, including 100% line and branch coverage and the Markdown
  checks.

## Out of scope

- Making `HLQ005` fire in a build; the package does not report it, and only
  the Visual Studio extension does.
- Enabling or tuning any other Hyperlinq rule; other `HLQ` rules stay at the
  analyzer's defaults everywhere.
- Changing the existing `Single()` calls in tests.
- Configuring the Visual Studio extension or removing it from anyone's
  machine.
- Adding other analyzers.

## Decisions

- The package is pinned in `Directory.Packages.props` and referenced from
  `Directory.Build.props`, mirroring how
  `Microsoft.VisualStudio.Threading.Analyzers` is wired, rather than a
  `Version` in `Directory.Build.props` (which conflicts with central package
  management).
- The version is pinned to 2.3.0, the version the earlier attempt observed.
- The maintainer's reply to "which way forward" was: fix all errors, use any
  rule needed to prove the gate, and postpone if no rule can be made to fire.
  The canary therefore uses whichever `HLQ` rule 2.3.0 actually reports,
  chosen by the implementer by probing and named in the commit message.
  `HLQ005` is excluded because it was shown not to fire.
- Stop condition: if no `HLQ` rule in 2.3.0 can be made to fire in a build, the
  implementer makes no change except to report that, and the item is postponed
  until a way of running the analyzer is found. AC-4 and AC-5 alone are not
  delivered in that case, since the maintainer wanted the gate and its
  proof together.
- Findings the analyzer reports are fixed in code, not suppressed, following
  the maintainer's rule that no errors or warnings are ignored.
