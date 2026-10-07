# Adopt ConfigureAwait.Fody and suppress the ConfigureAwait rules per project

## Summary

Visual Studio reports `ConfigureAwaitEnforcer` ("Consider using
ConfigureAwait(false).") as an error on every `await`, for example in
`src/AgenticSoftwareWorkflow.Cli/CommandLine.cs`. The rule comes from a
locally installed Visual Studio extension, not from this repository's
analyzers, so `dotnet build` and `scripts/gates.sh` never see it. The
maintainer has decided to adopt
[ConfigureAwait.Fody](https://github.com/Fody/ConfigureAwait), pinned in
`Directory.Packages.props`, so that code built by this workflow is correct
for hosts that have a `SynchronizationContext`. Each project declares the
Fody assembly attribute and suppresses the `ConfigureAwait` analyzer rules in
its own `GlobalSuppressions.cs`, with a justification, because Fody weaves
IL after compilation and source analyzers cannot see it. `.editorconfig` is
not used for this. Coverlet also rewrites IL, so the change must prove that
the 100% line and branch coverage gate still measures correctly.

## Acceptance criteria

- **AC-1** Given `Directory.Packages.props`, when it is read, then it contains
  exactly one `PackageVersion` for `ConfigureAwait.Fody` with an exact,
  non-floating version (no `*` and no version range), and that version is the
  one every project restores.
- **AC-2** Given each project under `src/` and under `tests/` except
  `tests/StyleCanary`, when it is built, then it references
  `ConfigureAwait.Fody` with `PrivateAssets="all"` (directly or through
  `Directory.Build.props`).
- **AC-3** Given each project named in AC-2, when its directory is read, then
  it contains a `GlobalSuppressions.cs` with the line
  `[assembly: Fody.ConfigureAwait(false)]`.
- **AC-4** Given each `GlobalSuppressions.cs` from AC-3, when it is read,
  then it also contains a
  `[assembly: SuppressMessage(...)]` for `ConfigureAwaitEnforcer` (category
  `"ConfigureAwait"`, check id
  `"ConfigureAwaitEnforcer:ConfigureAwaitEnforcer"`) and one for `CA2007`
  (category `"Usage"`), and each `Justification` is a real sentence stating
  that ConfigureAwait.Fody applies `ConfigureAwait(false)` after compilation,
  so analyzers that look for the call at every `await` cannot see it. A
  `Justification` of `"<Pending>"` or an empty string fails this criterion.
- **AC-5** Given `.editorconfig`, when it is read, then it contains no
  `dotnet_diagnostic.ConfigureAwaitEnforcer.severity` or
  `dotnet_diagnostic.CA2007.severity` entry.
- **AC-6** Given the built `AgenticSoftwareWorkflow.Cli` and
  `AgenticSoftwareWorkflow.Conductor` assemblies, when a test inspects an
  `await` in the compiled IL (for example the one in `CommandLine.cs`), then
  the awaited task has been configured with `ConfigureAwait(false)`. Fody is
  verified to have woven, not merely referenced.
- **AC-7** Given the Fody change, when `dotnet test` runs on every test
  project, then line and branch coverage are both still 100% and the existing
  threshold is unchanged. If coverage cannot be measured at 100% with Fody
  weaving in place, the work stops and reports the problem; it does not lower
  the threshold or exclude code.
- **AC-8** Given `scripts/gates.sh`, when it runs from WSL on the changed
  branch, then it passes with no gate, threshold or rule weakened.
- **AC-9** Given a source file in a project from AC-2 containing an `await`
  with no `ConfigureAwait` call, when the project is built with
  `dotnet build`, then the build emits no `CA2007` and no
  `ConfigureAwaitEnforcer` diagnostic.
- **AC-10** Given `docs/contribute/coding-standards.md`, when the "No
  `ConfigureAwait` noise" bullet is read, then it no longer says the project
  never needs `ConfigureAwait`. It states that every project declares
  `[assembly: Fody.ConfigureAwait(false)]` in `GlobalSuppressions.cs` via
  ConfigureAwait.Fody, that `ConfigureAwait(false)` is still never written at
  an `await`, and that the `ConfigureAwait` analyzer rules are suppressed in
  `GlobalSuppressions.cs` for the reason in AC-4. The file still passes
  markdownlint.
- **AC-11** Given a developer adding a new project, when they follow the
  coding standards, then the same place that tells them to add the Fody
  reference also tells them to add `GlobalSuppressions.cs`, and a check fails
  when a project under `src/` or `tests/` (other than `tests/StyleCanary`)
  lacks the package reference, the `Fody.ConfigureAwait(false)` attribute, or
  either suppression from AC-4. That check has a violation in
  `tests/StyleCanary` so `scripts/gates.sh verify` proves it fires.
- **AC-12** Given a project that omits `GlobalSuppressions.cs`, the attribute,
  or either suppression, when the check from AC-11 runs, then it fails and
  names the project and what is missing.

## Out of scope

- Writing `ConfigureAwait(false)` by hand at any `await`.
- Building or adopting a custom build-time analyzer that understands Fody's
  assembly attribute. None is known to exist; CA2007 does not understand it.
- Confirming that the errors are gone in Visual Studio. Only the maintainer
  can do that, on a machine with the extension installed.
- Enabling the rule on a per-project basis for future libraries.
- Suppressing the rules in `.editorconfig`.

## Decisions

- **Review: suppression moved from `.editorconfig` to `GlobalSuppressions.cs`.**
  The earlier draft disabled the rules with
  `dotnet_diagnostic.*.severity = none` in `.editorconfig`. The reviewer
  asked for per-project `GlobalSuppressions.cs` instead, with
  `[assembly: Fody.ConfigureAwait(false)]` and a `SuppressMessage` for
  `ConfigureAwaitEnforcer`. AC-5 now forbids the `.editorconfig` entries so
  the two mechanisms do not coexist, and the earlier AC-6 (`.editorconfig`
  contents) is replaced by AC-3 to AC-5.
- **Review: the assembly attribute lives in `GlobalSuppressions.cs`.**
  The reviewer's example puts `[assembly: Fody.ConfigureAwait(false)]` in that
  file, so AC-3 requires it there and in the fully qualified form shown,
  rather than in a separate file.
- **Review: the `"<Pending>"` justification is not accepted.** The
  reviewer's example uses it as a placeholder, but `CLAUDE.md` requires every
  suppression to carry a comment explaining why, so AC-4 requires a real
  justification. Check id and category for `ConfigureAwaitEnforcer` are copied
  from the review.
- **Review: the check (AC-11, AC-12) now looks for `GlobalSuppressions.cs`,**
  the attribute and both suppressions, instead of only the attribute and the
  package reference.
- **`CA2007` is suppressed as well as `ConfigureAwaitEnforcer`.** It is the
  built-in rule named earlier as the alternative and has the same blind spot.
  The review only named `ConfigureAwaitEnforcer`; this is my extension of it.
  Its category `"Usage"` is the one the built-in rule uses.
- **Rule suppressed, not left active.** The maintainer chose Fody so the rule
  can stay active for other projects this workflow builds. Their later
  experience was that Fody projects need a global suppression, because
  analyzers cannot see woven IL. The suppression is local to this repository's
  projects, so other projects can still enable the rules.
- **`tests/StyleCanary` is excluded.** It exists to hold deliberate
  violations and is not part of the shipped code. If it contains `await`s
  that Fody should weave, the implementer may include it and must say so.
- **Test projects are included.** The decision said "each project", and a
  test's `await` should behave like production code.
- **Enforcement is a repository check because no analyzer can do it.** The
  shape of the check (script, test, or MSBuild target) is left to the
  implementer, provided it is part of `scripts/gates.sh` and has a canary.
- **The Fody version is not chosen here.** The implementer pins the current
  stable release compatible with net10.0, as AC-1 requires.
