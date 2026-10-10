# Constructor injection with a single dependency-injection composition root

## Summary

Dependencies are chosen and constructed by hand in `Composition`, and some
classes in `AgenticSoftwareWorkflow.Conductor` still depend on a concrete
adapter (`GitRepository`) instead of an abstraction. This item makes
`Composition` (in `AgenticSoftwareWorkflow.Cli`) build its object graph with
`Microsoft.Extensions.DependencyInjection`, and makes every Conductor class
that performs I/O depend on an interface. `new` then remains only for values,
records, errors, and the composition root itself. Behaviour of the `aswf`
commands does not change.

## Acceptance criteria

- **AC-1**: Given the solution, when it is restored and built, then
  `AgenticSoftwareWorkflow.Cli` references the package
  `Microsoft.Extensions.DependencyInjection`, versioned the way the repository
  versions its other packages, and `AgenticSoftwareWorkflow.Conductor` does not
  reference it.
- **AC-2**: Given `Composition`, when `CreateSpecifyCommand`,
  `CreatePipeline`, or `CreateRunLoop` is called with valid
  `ConductorSettings`, then the returned object is resolved from a
  `ServiceProvider` that `Composition` built from a `ServiceCollection`, and
  the provider is built with `ValidateOnBuild` and `ValidateScopes` enabled.
- **AC-3**: Given `Composition` and settings with a non-empty `CodeGate`, when
  the service provider is built, then validation succeeds, so every
  registration's dependencies are themselves registered. This holds with no
  `Formatter`, an empty `Formatter`, and a non-empty `Formatter`.
- **AC-4**: Given settings whose `CodeGate` is `null` or empty, when
  `CreatePipeline(...).Build(id, token)` runs, then it fails with
  `BuildingNotConfigured`, exactly as today. Given a non-empty `CodeGate`, then
  it does not fail with `BuildingNotConfigured`.
- **AC-5**: Given settings with no `Formatter` or an empty one, when
  `BuildStage` is resolved, then its `IFormatting` is a `NoFormatter`. Given a
  non-empty `Formatter`, then it is a `CommandFormatter` for that command.
- **AC-6**: Given a resolved `BuildStage` and `BuildCommand` from the same
  provider, when their git dependency is compared, then it is the same
  instance. Likewise the one `IProcessCapable` (a `SystemProcessRunner`) is
  shared by every adapter that takes one.
- **AC-7**: Given the `AgenticSoftwareWorkflow.Conductor` assembly, when a test
  inspects the public constructors of its classes, then no parameter has a
  concrete class type that implements an interface declared in that assembly.
  `BuildStage`, `SpecifyCommand`, and `BuildCommand` therefore take a new
  interface in place of `GitRepository`, and `GitRepository` implements it.
  The interface is named after what it does, in the style of `IGateKeeping`
  and `IWorkSupplying`, and declares exactly the members those three classes
  use.
- **AC-8**: Given the `AgenticSoftwareWorkflow.Conductor` assembly, when a test
  inspects the types it declares, then none depends on
  `Microsoft.Extensions.DependencyInjection` (no service locator, no
  `IServiceProvider` parameter).
- **AC-9**: Given the source under `src/`, when it is searched for `new` of a
  type that is an adapter or service (`SystemProcessRunner`,
  `ClaudeCodeAgentRunner`, `CommandGate`, `CommandFormatter`, `NoFormatter`,
  `GitRepository`, `GitHubIssues`, `GitHubPullRequests`, `SpecifyStage`,
  `BuildStage`, `SpecifyCommand`, `BuildCommand`, `Pipeline`, `RunLoop`),
  then the only occurrences are inside the registration code of `Composition`
  (and its helper types in the same folder).
- **AC-10**: Given `Program.Main` with no arguments, when it runs, then it
  returns `CommandLine.UsageError` (64) and writes the usage text. Given the
  existing `CommandLineTest` cases for `specify`, `build`, and `run`, when they
  run against fake `IConductorComposing` implementations, then they pass
  unchanged, so exit codes and messages are as before.
- **AC-11**: Given `ConductorSettings.Load` fails (missing or unreadable
  `aswf.json`), when any command runs, then the failure is reported as today
  (`Failed (SettingsUnreadable): …`, exit code 1) and no service provider is
  built.
- **AC-12**: Given `docs/how-it-works/architecture.md`, when it is read, then
  it states that `Composition` is the single composition root, built on
  `Microsoft.Extensions.DependencyInjection`, that Conductor classes take
  interfaces through their constructors, and when `new` is acceptable. Any C#
  shown there comes from tests through `mdsnippets`.
- **AC-13**: Given the change, when `scripts/gates.sh` runs, then it passes
  with 100% line and branch coverage, and no gate, threshold, or rule has been
  weakened.

## Out of scope

- Shortening long parameter lists (#23), beyond what the new interface and
  registrations require.
- Introducing interfaces for orchestrating classes with no I/O of their own
  (`SpecifyStage`, `BuildStage`, `SpecifyCommand`, `BuildCommand`,
  `Pipeline`, `RunLoop`), or changing how they work.
- Changing the behaviour, output, exit codes, or settings format of any `aswf`
  command.
- Using the container in test projects, or replacing NSubstitute or hand-made
  fakes.
- Dependency injection in `tests/StyleCanary` beyond any lint violation this
  change itself needs.

## Decisions

- The triage mentions `CommandLine` receiving `Func<…, SpecifyCommand>`
  factories. The repository no longer has them: `CommandLine` already goes
  through `CommandContext.Composer` (`IConductorComposing`). I keep that seam
  so `CommandLine` tests need no change, and make `Composition` the container
  behind it.
- The container needs `ConductorSettings`, which are read at run time from
  `aswf.json`. The provider is therefore built inside `Composition`, per
  `Create…` call, after the settings load, not once at process start.
- Registrations are singletons, because the adapters hold no per-call state
  and today's code already shares one `SystemProcessRunner` and one
  `GitRepository` between `BuildStage` and `BuildCommand`.
- Dependencies that depend on settings (`GitHubOptions`, `GitIdentity`,
  base branch, gate commands) are registered with factory lambdas in
  `Composition`; no configuration or options packages are added.
- `TimeProvider.System` and the output `TextWriter` are registered, not
  created inside `RunLoop`'s caller.
- Only classes that reach outside the process are required to be interfaces
  (AC-7). Orchestrators stay concrete, because an interface with one
  implementation and no test double would add code without removing coupling.
- The new interface's exact name is left to the implementer, within the naming
  style named in AC-7.
- Providers are not disposed explicitly: nothing registered is
  `IDisposable`.
