# Hyperlinq analyzer as a gate; HLQ005 off in tests

## Summary

Visual Studio reports `HLQ005` ("Single() and SingleOrDefault() ... Use
First() or FirstOrDefault() instead") as an error in test projects. The rule
comes from a locally installed Visual Studio extension of
`NetFabric.Hyperlinq.Analyzer`, not from the repository, so `scripts/gates.sh`
never sees it. The maintainer decided that the Hyperlinq analyzer becomes a
real gate for all code, and that `HLQ005` is switched off for test projects,
because in tests `Single()` is the assertion that there is exactly one
element. This item adds the analyzer as a pinned package, fixes or justifies
whatever it reports in `src/`, turns `HLQ005` off for tests with the reason
recorded, and adds a canary violation so `scripts/gates.sh verify` proves the
gate fires.

## Acceptance criteria

- **AC-1** Given `Directory.Packages.props`, when it is read, then it contains
  a `PackageVersion` entry for `NetFabric.Hyperlinq.Analyzer` with an explicit
  fixed version (no range or wildcard), alongside the existing pinned
  packages.
- **AC-2** Given `Directory.Build.props`, when it is read, then it contains a
  `PackageReference` to `NetFabric.Hyperlinq.Analyzer` with
  `PrivateAssets="all"`, in the same item group as the existing
  `Microsoft.VisualStudio.Threading.Analyzers` reference, with no `Version`
  attribute (the version comes from `Directory.Packages.props`). The analyzer
  therefore runs in every project's build, including projects added later.
- **AC-3** Given a clean checkout, when `dotnet build` runs on the solution,
  then it succeeds with no Hyperlinq diagnostics: every `HLQ` finding in
  `src/` has been fixed in code, or suppressed with a comment explaining why
  the suppression is justified. No `HLQ` rule is disabled for `src/`.
- **AC-4** Given `.editorconfig`, when it is read, then the existing
  `[tests/**.cs]` section sets `dotnet_diagnostic.HLQ005.severity = none`,
  with a comment above it stating that in tests `Single()` is the assertion
  that there is exactly one element, so `First()` would weaken it.
- **AC-5** Given the test projects (including
  `tests/AgenticSoftwareWorkflow.Conductor.Test`) that call `Single()` (for
  example `ClaudeCodeAgentRunnerTest.cs`, `ClaudeOutputReaderTest.cs`,
  `CommandLineTest.cs`), when they are built, then no `HLQ005` diagnostic is
  reported, and those `Single()` calls are unchanged.
- **AC-6** Given a `.cs` file under `src/` that calls `Single()` on a
  sequence, when the project is built, then the build fails with `HLQ005`
  (the rule is not weakened outside tests).
- **AC-7** Given `tests/StyleCanary/Violations.cs`, when it is read, then it
  contains a line that violates an Hyperlinq rule, carrying the marker
  `// expect: build:HLQ005` in the format of the existing markers. Because the
  canary lives under `tests/`, `tests/StyleCanary/.editorconfig` re-enables
  the rule with `dotnet_diagnostic.HLQ005.severity = error` under `[*.cs]`,
  with a comment explaining why.
- **AC-8** Given the canary violation, when `scripts/gates.sh verify` runs,
  then it passes, with the build gate reporting `HLQ005` on the marked line.
- **AC-9** Given the canary violation, when the package reference from AC-2 is
  removed (or the analyzer is otherwise not run), then
  `scripts/gates.sh verify` fails because the expected `HLQ005` report is
  missing.
- **AC-10** Given the changes, when `scripts/gates.sh` runs (from WSL), then
  it passes in full, including 100% line and branch coverage and the
  Markdown checks.

## Out of scope

- Enabling or tuning any Hyperlinq rule other than `HLQ005` for tests; other
  `HLQ` rules stay at the analyzer's defaults everywhere.
- Changing the existing `Single()` calls in tests.
- Configuring the Visual Studio extension itself or removing it from anyone's
  machine.
- Adding other analyzers.

## Decisions

- The package is pinned in `Directory.Packages.props` and referenced from
  `Directory.Build.props`, mirroring how
  `Microsoft.VisualStudio.Threading.Analyzers` is wired in this repository,
  rather than the maintainer's wording "pinned package in
  `Directory.Build.props`" taken literally (a `Version` there would conflict
  with central package management).
- The latest stable `NetFabric.Hyperlinq.Analyzer` version compatible with
  the `net10.0` SDK is chosen by the implementer at the time of the change.
- `HLQ005` is used for the canary because it is the rule the maintainer
  named, and it is trivial to trigger with `Single()`. It needs re-enabling in
  `tests/StyleCanary/.editorconfig` since the `[tests/**.cs]` section would
  otherwise silence it for the canary.
- The `HLQ005` findings in `src/`, if any, are fixed by switching to
  `First()`/`FirstOrDefault()` only where exactly-one validation is not
  intended; where it is intended, a suppression with a justifying comment is
  used, per the repository rule that every suppression carries a comment.
