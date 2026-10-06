# Gate: every test follows Arrange/Act/Assert (or Given/When/Then)

## Summary

The Arrange/Act/Assert rules in `docs/contribute/coding-standards.md` are only checked in review. This item makes them a gate for C# tests (a new Roslyn analyzer, reported through the existing `build` gate) and for Python tests (a new pylint checker in `scripts/lint/`, registered from `scripts/lint/house_rules.py`, reported through the existing `pylint` gate). Both implement one rule: markers are collected from a test and from the same-file helpers it calls, in call order, and the combined sequence must be valid. Canary violations in `tests/StyleCanary` prove each failure is caught, and `scripts/gates.sh verify` must fail if any is missed. All existing tests in the repository must pass the new gate.

## Definitions (shared by both languages)

- **Test**: C# is a method carrying `[Fact]` or `[Theory]` (or an attribute deriving from either). Python is a function named `test_*`, at module level or in a class.
- **Marker**: a comment whose text, after the comment leader (`//` or `#`) and surrounding whitespace, is exactly one of `Arrange`, `Act`, `Assert`, `Given`, `When`, `Then`, `Sanity Check`. Matching is case-sensitive. A comment such as `// Arrange the thing` is not a marker; it is an ordinary comment.
- **Role**: `Arrange`≡`Given`, `Act`≡`When`, `Assert`≡`Then`. `Sanity Check` has no vocabulary and is allowed with either.
- **Helper**: a method (C#, same class) or function/method (Python, same module) that the test calls by simple name: `Helper(...)` or `this.Helper(...)` in C#, `helper(...)` or `self.helper(...)` in Python. In C# a helper is chosen by name and argument count. Calls to anything defined outside the file are ignored.
- **Combined sequence**: the markers of the test's body, with each helper call replaced, at the position of the call, by that helper's own combined sequence. This is transitive. A helper called twice contributes twice. A helper already being expanded further up the call chain is not expanded again, so recursion terminates.

## Acceptance criteria

### Valid sequences (no finding)

1. Given a test whose combined sequence is `Arrange, Act, Assert`, when the gate runs, then no finding is reported for it.
2. Given a test whose combined sequence is `Given, When, Then`, when the gate runs, then no finding is reported.
3. Given a test with `Arrange, Sanity Check, Act, Assert` (or `Given, Sanity Check, When, Then`), when the gate runs, then no finding is reported.
4. Given a test whose Arrange marker is followed by an ordinary comment explaining why there is nothing to arrange (for example `// Nothing to arrange: the role catalogue is static.`), then `Act` and `Assert`, when the gate runs, then no finding is reported.
5. Given a test whose Arrange marker is followed by at least one statement before `Act`, when the gate runs, then it is not treated as an empty Arrange.
6. Given a test with no markers of its own that calls a helper in the same class or module which supplies `Arrange, Act, Assert`, when the gate runs, then no finding is reported.
7. Given a test that has `// Arrange` itself, calls a helper that supplies `Act, Assert`, when the gate runs, then no finding is reported. The call-order position of the helper's markers is what counts.
8. Given a helper that calls a second helper supplying part of the sequence, when the gate runs, then the markers are collected transitively and a valid combined sequence passes.
9. Given a method that is not a test (no `[Fact]`/`[Theory]`; a Python function not named `test_*`) and has no markers, when the gate runs, then no finding is reported for it.

### Invalid sequences

Each rule below is reported independently, so one test can produce several findings. Roles are used for criteria 10 to 13, so `Arrange, When, Then` is a mixed-vocabulary finding only.

10. **Missing marker.** Given a test whose combined sequence lacks the Arrange role, the Act role, or the Assert role, when the gate runs, then one finding of rule `aaa-missing-marker` (C# diagnostic `AAA001`) is reported on the test, and its message names each missing role.
11. **Missing marker through delegation.** Given a test that calls a same-file helper whose markers do not complete the sequence (for example the test has `// Arrange` and the helper supplies only `// Act`), when the gate runs, then `aaa-missing-marker` is reported on the test (not on the helper), naming `Assert`.
12. **Wrong order.** Given a combined sequence in which the first occurrence of one role precedes the first occurrence of an earlier role (for example `Act, Arrange, Assert`), or a `Sanity Check` appears before Arrange or after Act, when the gate runs, then one finding of rule `aaa-wrong-order` (`AAA002`) is reported on the test.
13. **Duplicate.** Given a combined sequence in which any role appears more than once, or `Sanity Check` appears more than once (including a helper called twice that supplies a marker), when the gate runs, then one finding of rule `aaa-duplicate-marker` (`AAA003`) is reported on the test.
14. **Mixed vocabularies.** Given a combined sequence containing at least one Arrange/Act/Assert marker and at least one Given/When/Then marker (for example `Arrange, When, Then`), when the gate runs, then one finding of rule `aaa-mixed-vocabulary` (`AAA004`) is reported on the test.
15. **Unexplained empty Arrange.** Given a test (or helper) whose Arrange or Given marker is followed directly by the next marker, or by nothing at all, within the same body, with no statement and no ordinary comment between them, when the gate runs, then one finding of rule `aaa-unexplained-empty-arrange` (`AAA005`) is reported on the test containing or reaching that marker.
16. **Empty test.** Given a test with no markers of its own that calls no helper, or only helpers with no markers, when the gate runs, then `aaa-missing-marker` is reported naming all three roles.
17. **Recursion.** Given helpers that call each other in a cycle, when the gate runs, then the gate terminates and judges the sequence collected before the cycle closed.

### Reporting and wiring

18. Findings are reported at the line of the test method name (C# identifier, Python `def`). The `StyleCanary` `expect: <gate>:<rule>` marker goes on that line. The C# gate name is `build`, the Python gate name is `pylint`, and the rule is the id above (`AAA001` etc. for C#, the `aaa-*` symbol for Python).
19. The C# analyzer runs on every test project under `tests/` including `tests/StyleCanary`, and is built as an analyzer project referenced as an analyzer (not shipped in `src/`). Its diagnostics are errors, so `scripts/gates.sh` fails on any of them. Source projects under `src/` are not checked.
20. The Python checker is registered in `register()` in `scripts/lint/house_rules.py` and applies to Python test modules in the repository, enabled in the same way as the other house rules. Its module is split by intent like its neighbours.
21. `tests/StyleCanary/Violations.cs` and `tests/StyleCanary/violations.py` each gain one canary per failure: a missing marker, wrong order, a duplicate, mixed vocabularies, an unexplained empty Arrange, and a test whose helper does not complete the sequence (the last expects the missing-marker rule). Each carries an `expect:` marker. Given the checker or analyzer is disabled, when `scripts/gates.sh verify` runs, then it prints `[MISSED]` for those canaries and exits non-zero. Given everything is enabled, it reports them all and exits zero.
22. Both implementations carry their own unit tests covering every criterion above, and the repository's 100% line and branch coverage requirement holds for them.
23. Every existing C# and Python test in the repository passes the new gate when the work lands. Any that do not are fixed to the rule, never exempted.
24. `docs/contribute/coding-standards.md` and `docs/how-it-works/quality-gates.md` are updated to state that the rule is now enforced, and to document the Given/When/Then alternative and delegation. Any C# shown comes from tests via `mdsnippets`.

## Out of scope

- Checking that the Act is a single statement (a stated follow-up).
- Checking the content of Arrange, Act or Assert blocks, or that a test asserts anything.
- Helpers defined in other files, base classes or other modules; calls through delegates, lambdas or reflection.
- Other test frameworks than xUnit (C#) and `test_*` functions (Python); markers in non-test code.
- Shell, Markdown or YAML tests.

## Decisions

- **C# analyzer location**: no analyzer project exists in the repository today (item #3 has not landed), so this item creates one, designed so #3 can share it. It is a new project, referenced by test projects only.
- **Rule names**: the item does not name them. I chose `AAA001`–`AAA005` for C# and `aaa-missing-marker`, `aaa-wrong-order`, `aaa-duplicate-marker`, `aaa-mixed-vocabulary`, `aaa-unexplained-empty-arrange` for Python, since `verify_gates.py` matches a finding by a single rule string per `expect:` marker.
- **Helper failure reporting**: a helper that does not complete the sequence is reported as the existing missing-marker rule, on the test, rather than a sixth rule, because the failure is the same.
- **Marker matching**: exact, case-sensitive text so that prose comments like `// Arrange the user` are not mistaken for markers. Sanity Check is allowed at most once and only between Arrange and Act.
- **Vocabulary by role**: markers are mapped to roles so that a mix is reported once as mixed, not additionally as missing.
- **Findings are independent**: a test breaking several rules gets several findings, so each canary can prove its own rule.
- **Helper resolution**: by simple name (and argument count in C#), with `this.`/`self.` accepted, because the project's standards require `this.` on instance members.
- **Empty Arrange across bodies**: judged within a single body. An Arrange marker at the end of a body, followed only by nothing, is empty; a helper call after it counts as a statement.
- **Scope**: the rule applies to `tests/` code only, because production code has no tests of this form.
