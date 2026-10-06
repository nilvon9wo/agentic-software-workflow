# Make NetFabric.Hyperlinq.Analyzer a build gate; turn HLQ005 off for tests

## Summary

Visual Studio shows `HLQ005` errors in the test projects. They come from the locally installed NetFabric.Hyperlinq extension, not from the repo, so `scripts/gates.sh` never fails on them. The maintainer decided two things:

- The Hyperlinq analyzer becomes a real gate, delivered as a pinned `NetFabric.Hyperlinq.Analyzer` package that every project builds with.
- `HLQ005` is turned off for test projects. There `Single()` is the assertion that exactly one element exists, and `First()` would weaken it.

## Acceptance criteria

1. Given `Directory.Packages.props`, when read, then it contains a `PackageVersion` for `NetFabric.Hyperlinq.Analyzer` with one exact version, not a range or a floating version. Central package management is the existing convention.
2. Given `Directory.Build.props`, when read, then its `ItemGroup` of repo-wide analyzers contains `<PackageReference Include="NetFabric.Hyperlinq.Analyzer" PrivateAssets="all" />` with no `Version` attribute, matching the existing `Microsoft.VisualStudio.Threading.Analyzers` entry. A comment above it says why it is there.
3. Given any project in the solution, including the `src/` and `tests/` projects, when `dotnet build` runs, then the Hyperlinq analyzer runs. The package is a build-time analyzer only and is not a runtime dependency of the built output.
4. Given the `src/` projects, when `dotnet build` runs, then there are zero Hyperlinq diagnostics (`HLQ*`). Each one the analyzer reports is either fixed in code or suppressed at the narrowest scope with a comment explaining why. No `HLQ` rule is disabled globally or for `src/`. A rule the maintainer rejects for production code is a "say so and stop" case, not something to turn off quietly.
5. Given `.editorconfig`, when read, then the existing `[tests/**.cs]` section sets `dotnet_diagnostic.HLQ005.severity = none`. A comment above it says that in tests `Single()` is the assertion that exactly one element exists, so `First()` would weaken it.
6. Given the three reported sites (`ClaudeCodeAgentRunnerTest.cs:221`, `ClaudeOutputReaderTest.cs:65`, `CommandLineTest.cs:130`), and any other `Single()` or `SingleOrDefault()` in the test projects, when `dotnet build` runs, then none produces `HLQ005`. The tests keep using `Single()`. No test is changed to `First()`.
7. Given a test project that triggers a Hyperlinq rule other than `HLQ005`, when `dotnet build` runs, then it is still reported, and so fails the build under `TreatWarningsAsErrors`. Only `HLQ005` is exempted.
8. Given `tests/StyleCanary/Violations.cs`, when read, then it has a new violation of a Hyperlinq rule that fires on `src/`-style code. It carries a marker `// expect: build:HLQ005`, or `build:` plus another `HLQ` rule if HLQ005 cannot be made to fire there. The marker follows the existing `// expect: <gate>:<rule>` convention.
9. Given the StyleCanary project is under `tests/`, when it builds, then `HLQ005` is still reported at error severity there. `tests/StyleCanary/.editorconfig` re-enables the rule for the canary only (`dotnet_diagnostic.HLQ005.severity = error`), with a comment saying the canary must prove the rule fires despite the `tests/**` exemption.
10. Given the new canary violation, when `scripts/gates.sh verify` runs (`scripts/verify_gates.py`), then it passes. If the analyzer package stops being referenced, or `HLQ005` is silently turned off for the canary, then it fails because the expected `build:HLQ005` report is missing.
11. Given all of the above, when `scripts/gates.sh` runs, then it passes, with 100% line and branch coverage unchanged.

## Out of scope

- The Visual Studio extension itself. It is a local install and the repo does not manage it.
- Changing `Single()` to `First()` anywhere.
- Hyperlinq's runtime library (`NetFabric.Hyperlinq`) and adopting its APIs. Only the analyzer package is added.
- Turning off any other `HLQ` rule for tests. Other rules are exempted only on a later, separate decision.
- Adding any other IDE-extension analyzer as a gate.

## Decisions

- **Package version.** The maintainer did not name one, so the implementer pins the latest stable `NetFabric.Hyperlinq.Analyzer` available at implementation time. The comment in `Directory.Packages.props` says whether the version is a beta, following the existing precedent.
- **Where the exemption lives.** The existing `[tests/**.cs]` section of the root `.editorconfig` is reused. It already holds the test-only `CA1859` exemption, so no new section is needed.
- **Canary override.** The canary lives under `tests/`, so the test exemption would silence its `HLQ005` violation. I decided it is re-enabled in the canary's own `.editorconfig`, which already holds canary-only severity overrides. Without this, criterion 8 could never pass.
- **Which rule the canary uses.** `HLQ005` is preferred because it is the rule this item concerns. The implementer may use a different `HLQ` rule if `HLQ005` does not fire on plain `Violations.cs` code.
- **Severity.** The analyzer's default warning severity is promoted to an error by the existing `TreatWarningsAsErrors`. No per-rule severity lines are added for rules in `src/`, because that would duplicate this.
- **Reporting.** If, while fixing `src/`, the implementer finds a Hyperlinq rule that is wrong for this codebase, they must report it and stop, as `CLAUDE.md` requires. They must not demote it.
