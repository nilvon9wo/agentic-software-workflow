# Gate: every test follows Arrange/Act/Assert (or Given/When/Then)

## Summary

The Arrange/Act/Assert rules in `docs/contribute/coding-standards.md` are only
checked in review. This item makes them a gate for C# tests (a Roslyn
analyzer) and Python tests (a house-rule pylint checker in
`scripts/lint/house_rules.py`), so that `scripts/gates.sh` fails on a test
whose marker comments are missing, duplicated, out of order, mixed, or
unexplained. Canary violations in `tests/StyleCanary` prove each failure is
caught by `scripts/gates.sh verify`.

## Acceptance criteria

Terms used below. A *test* is a C# method carrying `[Fact]` or `[Theory]`, or
a Python function or method whose name starts with `test_`. A *marker* is a
comment whose text, after the comment opener (`//` or `#`) and surrounding
whitespace are removed, equals exactly one of `Arrange`, `Act`, `Assert`,
`Sanity Check`, `Given`, `When`, or `Then` (case-sensitive). The *sequence* of
a test is its markers in source order, plus, at each call to a helper, the
helper's own sequence at that point. A *helper* is a method of the same
class (C#) or a function of the same module, or method of the same class
(Python), that the test calls; helpers are followed transitively, within the
file only, each helper expanded once per call site, and a helper already
being expanded is not expanded again (so recursion terminates).

- **AC-1**: Given a C# test whose sequence is `Arrange`, `Act`, `Assert`,
  each exactly once and in that order, when the analyzer runs, then it
  reports nothing for that test.
- **AC-2**: Given a C# test whose sequence is `Given`, `When`, `Then`, each
  exactly once and in that order, when the analyzer runs, then it reports
  nothing.
- **AC-3**: Given a C# test whose sequence is `Arrange`, `Sanity Check`,
  `Act`, `Assert`, when the analyzer runs, then it reports nothing.
- **AC-4**: Given a C# test whose sequence lacks one of `Arrange`, `Act`,
  `Assert` (or `Given`, `When`, `Then`), when the analyzer runs, then it
  reports a diagnostic `ASWF0010` at that test's method name, with a message
  naming the missing marker.
- **AC-5**: Given a C# test where a marker appears more than once, when the
  analyzer runs, then it reports `ASWF0011`, naming the duplicated marker.
- **AC-6**: Given a C# test whose markers are all present once but out of
  order (for example `Act` before `Arrange`), when the analyzer runs, then it
  reports `ASWF0012`.
- **AC-7**: Given a C# test that mixes vocabularies (for example `Arrange`,
  `When`, `Assert`), when the analyzer runs, then it reports `ASWF0013`.
- **AC-8**: Given a C# test where `Arrange` (or `Given`) is followed directly
  by the next marker, with no statement and no explanatory comment line
  between them, when the analyzer runs, then it reports `ASWF0014`. Given the
  same test with a non-marker comment line (for example
  `// Nothing to arrange: the role catalogue is static.`) between them, then
  it reports nothing.
- **AC-9**: Given a C# test with no markers of its own that calls a helper in
  the same class whose sequence is `Arrange`, `Act`, `Assert`, when the
  analyzer runs, then it reports nothing.
- **AC-10**: Given a C# test that calls helpers whose combined sequence, in
  call order, together with the test's own markers, is invalid (for example
  the helper supplies only `Arrange` and `Act`), when the analyzer runs, then
  it reports the diagnostic for that failure (`ASWF0010` for this example) at
  the test's method name.
- **AC-11**: Given a C# method with no `[Fact]` or `[Theory]` attribute and
  no markers, when the analyzer runs, then it reports nothing for it.
- **AC-12**: Given a Python test whose sequence is valid under AC-1, AC-2 or
  AC-3 (with `#` comments), when pylint runs with the house-rule checker,
  then it reports nothing.
- **AC-13**: Given a Python test exhibiting each failure of AC-4 to AC-8, when
  pylint runs, then it reports one message per failure, with the same
  meanings as `ASWF0010` to `ASWF0014`, at the test's `def` line. The message
  symbols are `missing-aaa-marker`, `duplicate-aaa-marker`,
  `misordered-aaa-marker`, `mixed-aaa-vocabulary`, and
  `unexplained-empty-arrange`.
- **AC-14**: Given a Python test with no markers of its own that calls a
  helper function in the same module or a `self.` method in the same class
  whose sequence is valid, when pylint runs, then it reports nothing; given a
  helper whose sequence leaves the combined sequence incomplete, then it
  reports `missing-aaa-marker` at the test.
- **AC-15**: Given a Python function not named `test_*`, when pylint runs,
  then it reports nothing for it.
- **AC-16**: Given the canary files `tests/StyleCanary/Violations.cs` and
  `tests/StyleCanary/violations.py`, when `scripts/gates.sh verify` runs, then
  each contains one violation for every failure listed in AC-4 to AC-8 plus a
  helper that does not complete the sequence (AC-10), and verification
  passes only if every one is reported; removing any such canary or
  disabling any rule makes `verify` fail.
- **AC-17**: Given the repository's existing C# and Python tests, when
  `scripts/gates.sh` runs, then the new rules report nothing (existing tests
  are corrected where they violate them), and the rules' own code meets 100%
  line and branch coverage.

## Out of scope

- Checking that the Act is a single statement (a possible follow-up, since it
  is hard to judge statically when helpers are involved).
- Checking markers across files, or helpers outside the test's own class or
  module.
- Shell and Markdown tests; fixtures and non-test setup methods.
- Changing `coding-standards.md` rules other than documenting the gate.

## Decisions

- **Diagnostic IDs and symbols**: `ASWF0010` to `ASWF0014` for C#, and the
  five pylint symbols in AC-13, are chosen here because no analyzer project or
  ID scheme exists yet. If item #3 creates a shared analyzer project first,
  these IDs move into it without changing meaning.
- **Marker matching** is exact and case-sensitive on the comment text, so
  `// Arrange the roles` is not a marker. This matches the "verbatim" rule in
  the coding standards.
- **"Explained" empty Arrange** means at least one non-marker comment line
  between `Arrange`/`Given` and the next marker. A statement there means it is
  not empty. An `Arrange` directly followed by `Sanity Check` counts as empty
  and unexplained.
- **Failure precedence** when a test has several faults: the analyzer reports
  each applicable diagnostic once per test, except that out-of-order is not
  reported when a marker is missing or duplicated.
- **Helpers are expanded per call site**, so a helper called twice
  contributes its markers twice, which makes a duplicate visible.
- **Scope of C# analysis** is test projects only (`tests/`); the analyzer is
  not applied to production projects.
