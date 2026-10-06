# Adopt ConfigureAwait.Fody and silence the IDE-only ConfigureAwaitEnforcer rule

## Summary

Visual Studio reports `ConfigureAwaitEnforcer` ("Consider using
ConfigureAwait(false).") as an error on every `await`, for example in
`src/AgenticSoftwareWorkflow.Cli/CommandLine.cs`. The rule comes from a
locally installed Visual Studio extension, not from this repository's
analyzers, so `dotnet build` and `scripts/gates.sh` never see it. The
maintainer has decided to adopt
[ConfigureAwait.Fody](https://github.com/Fody/ConfigureAwait), pinned in
`Directory.Packages.props`, with `[assembly: ConfigureAwait(false)]` in each
project, so that code built by this workflow is correct for hosts that have a
`SynchronizationContext`. Fody weaves IL after compilation, so source
analyzers cannot see it. The repository therefore also disables the
build-time and IDE `ConfigureAwait` rules in `.editorconfig`, with a comment
saying why. Coverlet also rewrites IL, so the change must prove that the 100%
line and branch coverage gate still measures correctly.

## Acceptance criteria

- **AC-1** Given `Directory.Packages.props`, when it is read, then it contains
  exactly one `PackageVersion` for `ConfigureAwait.Fody` with an exact,
  non-floating version (no `*` and no version range), and that version is the
  one every project restores.
- **AC-2** Given each project under `src/` and under `tests/` except
  `tests/StyleCanary`, when it is built, then it references
  `ConfigureAwait.Fody` with `PrivateAssets="all"` (directly or through
  `Directory.Build.props`) and carries the assembly attribute
  `[assembly: ConfigureAwait(false)]`.
- **AC-3** Given the built `AgenticSoftwareWorkflow.Cli` and
  `AgenticSoftwareWorkflow.Conductor` assemblies, when a test inspects an
  `await` in the compiled IL (for example the one in `CommandLine.cs`), then
  the awaited task has been configured with `ConfigureAwait(false)`. Fody is
  verified to have woven, not merely referenced.
- **AC-4** Given the Fody change, when `dotnet test` runs on every test
  project, then line and branch coverage are both still 100% and the existing
  threshold is unchanged. If coverage cannot be measured at 100% with Fody
  weaving in place, the work stops and reports the problem; it does not lower
  the threshold or exclude code.
- **AC-5** Given `scripts/gates.sh`, when it runs from WSL on the changed
  branch, then it passes with no gate, threshold or rule weakened.
- **AC-6** Given `.editorconfig`, when it is read, then it sets
  `dotnet_diagnostic.ConfigureAwaitEnforcer.severity = none` and
  `dotnet_diagnostic.CA2007.severity = none`, each directly under a comment
  stating that ConfigureAwait.Fody applies `ConfigureAwait(false)` after
  compilation, so source analyzers that look for the call at every `await`
  cannot see it.
- **AC-7** Given a source file containing an `await` with no
  `ConfigureAwait` call, when the project is built with `dotnet build`, then
  the build emits no `CA2007` and no `ConfigureAwaitEnforcer` diagnostic.
- **AC-8** Given `docs/contribute/coding-standards.md`, when the "No
  `ConfigureAwait` noise" bullet is read, then it no longer says the project
  never needs `ConfigureAwait`. It states that every project declares
  `[assembly: ConfigureAwait(false)]` via ConfigureAwait.Fody, that
  `ConfigureAwait(false)` is still never written at an `await`, and that the
  `ConfigureAwait` analyzer rules are disabled for the reason in AC-6. The
  file still passes markdownlint.
- **AC-9** Given a developer writing a new project in this repository, when
  they follow the coding standards, then they find the instruction to add the
  Fody reference and assembly attribute in the same place, so a new project
  cannot be added without it. The expected mechanism is a check that fails
  when a project under `src/` or `tests/` (other than `tests/StyleCanary`)
  lacks the attribute or the package reference. That check has a violation
  in `tests/StyleCanary` so `scripts/gates.sh verify` proves it fires.
- **AC-10** Given a project that omits the attribute, when the check from AC-9
  runs, then it fails and names the project.

## Out of scope

- Writing `ConfigureAwait(false)` by hand at any `await`.
- Building or adopting a custom build-time analyzer that understands Fody's
  assembly attribute. None is known to exist; CA2007 does not understand it.
- Confirming that the errors are gone in Visual Studio. Only the maintainer
  can do that, on a machine with the extension installed.
- Enabling the rule on a per-project basis for future libraries.

## Decisions

- **Rule disabled, not left active.** The maintainer chose Fody "so the rule
  can stay active" for other projects this workflow builds. Their later
  experience was that Fody projects need a global suppression, because
  analyzers cannot see woven IL. I follow that: this repository's
  `.editorconfig` suppresses both rules, and other projects can still enable
  them.
- **`CA2007` is suppressed as well as `ConfigureAwaitEnforcer`.** CA2007 is
  the built-in rule the earlier discussion named as the alternative, and it
  would have the same blind spot.
- **`tests/StyleCanary` is excluded.** It exists to hold deliberate
  violations and is not part of the shipped code. If it contains `await`s
  that Fody should weave, the implementer may include it and must say so.
- **Test projects are included.** The decision said "each project", and a
  test's `await` should behave like production code.
- **Enforcement is a repository check (AC-9) because no analyzer can do it.**
  The shape of the check (script, test, or MSBuild target) is left to the
  implementer, provided it is part of `scripts/gates.sh` and has a canary.
- **The Fody version is not chosen here.** The implementer pins the current
  stable release compatible with net10.0, as AC-1 requires.
