# Add mutation testing (Stryker.NET) as a gate

## Summary

100% line and branch coverage proves code ran, not that the tests would
notice a bug. This item adds a `mutation` gate that runs Stryker.NET (already
pinned as `dotnet-stryker` 5.0.0 in `dotnet-tools.json`) over the production
projects and fails when the mutation score falls below a break threshold kept
in a Stryker configuration file. The gate is registered beside the `test`
gate, so `scripts/gates.sh run` (and therefore CI) runs it. A canary proves
it: `scripts/gates.sh verify` adds a throwaway class that is fully covered
but whose test asserts nothing, and the gate must then fail.

## Acceptance criteria

- **AC-1** Given the registry in `scripts/gates/registry.py`, when
  `ALL_GATES` in `scripts/run_gates.py` is read, then it contains a `Gate`
  named `mutation`, placed after the `test` gate and before the `pytest`
  gate, and `scripts/gates.sh run mutation` runs only that gate.
- **AC-2** Given `stryker-config.json` at the repository root, when it is
  read, then it sets `thresholds.break` to a whole number greater than 0,
  `thresholds.high` and `thresholds.low` to whole numbers not below `break`,
  and names the test project
  `tests/AgenticSoftwareWorkflow.Conductor.Test` and the mutated projects
  `src/AgenticSoftwareWorkflow.Conductor` and
  `src/AgenticSoftwareWorkflow.Cli`.
- **AC-3** Given the `mutation` gate runs against the real repository,
  when Stryker finishes with a score at or above `thresholds.break`, then
  the `GateResult` has exit code 0 and no findings, and `has_passed` is
  true.
- **AC-4** Given Stryker finishes with a score below `thresholds.break`,
  when the gate reports, then Stryker's non-zero exit code is carried into
  the `GateResult`, `has_passed` is false, and the gate prints the tool
  output.
- **AC-5** Given Stryker's JSON report lists mutants with status
  `Survived`, when the gate parses it, then it yields one `Finding` per
  survivor with `gate="mutation"`, `rule` the mutator name (for example
  `Arithmetic`), `file_name` the source file's base name, and `line_number`
  the mutant's start line; mutants with any other status yield none.
- **AC-6** Given Stryker writes no report (for example it crashed or found
  no mutants to test), when the gate parses the result, then it yields no
  findings and the non-zero exit code alone fails the gate, with Stryker's
  output shown; a missing report never reads as success.
- **AC-7** Given `scripts/verify_gates.py` runs, when it exercises the
  mutation gate, then it writes a throwaway production class and a
  throwaway test that calls it without asserting its result (so the `test`
  gate's 100% coverage still holds), runs the `mutation` gate, and requires
  that the gate fails; it prints
  `mutation gate caught a surviving mutant: True`.
- **AC-8** Given the throwaway files from AC-7, when the check ends,
  whether the gate failed or raised, then both files are deleted, and the
  verify run exits non-zero if the gate passed.
- **AC-9** Given Stryker writes reports and caches while it runs, when the
  gate finishes, then no `StrykerOutput` directory or other Stryker artefact
  is left tracked or untracked in the working tree (it is written to a
  directory that is git-ignored or temporary).
- **AC-10** Given a clean checkout at the break threshold, when
  `scripts/gates.sh run` is executed, then the `mutation` gate passes on the
  repository as committed, with `thresholds.break` set to the score measured
  when the item is implemented, rounded down to a whole number.
- **AC-11** Given `docs/how-it-works/quality-gates.md` and
  `docs/contribute/coverage-standards.md`, when they are read, then the
  gate table lists `mutation`, the "Known gaps" line saying Stryker.NET is
  "not yet a gate" is removed, the coverage standard no longer says the gate
  is "planned", and both state the ratchet rule: the break threshold may be
  raised but never lowered to make work pass.

## Out of scope

- Raising the threshold beyond the measured starting score, and killing
  surviving mutants in existing code.
- Mutation testing the Python scripts.
- Running only on changed files (`--since`); see Decisions.
- Mutating `tests/` projects, `tests/StyleCanary`, or the test-process
  helper project.
- The "hidden tests" and arbitrator stages named in the architecture
  document.

## Decisions

- **Every PR runs the whole mutation gate, not only changed files.** CI runs
  exactly `scripts/gates.sh` and "nothing only CI can fail". A `--since`
  mode would make the local and CI results differ, and would need a base
  ref the worker environment may lack. The codebase is small; if the
  runtime proves too costly, a follow-up item can add incremental mode.
- **The starting threshold is measured at implementation time.** The current
  score cannot be known without running Stryker, so AC-10 fixes the rule
  (measured score, rounded down) rather than a number. The implementer
  records the figure in `stryker-config.json`.
- **Configuration lives in `stryker-config.json` at the repository root.**
  This is Stryker's default file name; the gate does not pass thresholds on
  the command line, so the ratchet is one visible line in a reviewed file.
- **Survivors are reported as findings** from Stryker's JSON reporter, so a
  failing run names `file:line` and mutator, like the other gates, rather
  than only a score.
- **The canary adds throwaway files to the real source and test trees**,
  as the existing coverage canaries do, because Stryker mutates real
  projects; `tests/StyleCanary` is not a mutation target. The throwaway test
  must give the new class full line and branch coverage so that only the
  mutation gate, not the `test` gate, is what fails.
- **The gate is in `ALL_GATES` but not in `STATIC_GATES`**, because it runs
  code; the `unproven_gates` check, which demands a static canary marker,
  therefore does not apply, and AC-7 provides its proof instead.
