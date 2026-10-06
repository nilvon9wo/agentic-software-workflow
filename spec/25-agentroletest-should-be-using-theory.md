# Parameterise tests that differ only by data, and gate against new ones

## Summary

`AgentRoleTest` holds two tests, `CanEdit_WhenGrantedEditFiles_IsTrue` and
`CanEdit_WhenNotGrantedEditFiles_IsFalse`. They differ only in the tools given
to `AgentRole` and the boolean expected from `CanEdit`.
`docs/contribute/coding-standards.md` already asks for `[Theory]` for
data-row variations, but nothing enforces it. This item does three things.
It merges that pair into one `[Theory]`. It finds and merges every other set
of tests in `tests/` that differ only by their data. It adds a gate to
`scripts/gates.sh`, proven by a canary, so a new set of such tests fails the
build instead of waiting for a reviewer to notice.

## Acceptance criteria

- **AC-1**: Given `AgentRoleTest.cs`, when it is read, then it contains one
  `[Theory]` for `AgentRole.CanEdit` and neither
  `CanEdit_WhenGrantedEditFiles_IsTrue` nor
  `CanEdit_WhenNotGrantedEditFiles_IsFalse` exists as a `[Fact]`.
- **AC-2**: Given the `CanEdit` theory, when it runs, then it has a row where
  the tools are `ReadFiles` and `EditFiles` and the expected value is `true`.
  It also has a row where the tools are `ReadFiles` and `SearchFiles` and the
  expected value is `false`. Both rows use `CapabilityTier.Standard` and
  `AgentAccess.ToolsOnly`, as the two original tests did.
- **AC-3**: Given the `CanEdit` theory, when `AgentRole.CanEdit` is changed to
  always return `true`, then the theory fails. When it is changed to always
  return `false`, then the theory fails. Neither original case is lost.
- **AC-4**: Given the `CanEdit` theory, when it is read, then it follows
  `coding-standards.md`. That means the name has the form
  `<Method>_When<Condition>_<Outcome>`, the `// Arrange`, `// Act` and
  `// Assert` comments are present, the Act is one statement, and it uses
  `this.` and explicit types.
- **AC-5**: Given every test class under `tests/` other than `StyleCanary`,
  when the new gate runs, then it reports no set of tests that differ only by
  literal values or enum members. Each set it found in the existing tests has
  been merged into a `[Theory]` (or a `[Fact]` per case calling a shared
  private runner, where xUnit parameterisation does not fit).
- **AC-6**: Given any merged set from AC-5, when the tests run, then each
  original case is still a row or a case. No assertion is weakened, and no case
  is dropped to make the merge easier.
- **AC-7**: Given a new gate named `duplicates` in `scripts/gates/`, when
  `scripts/gates.sh run duplicates` runs on the repository, then it exits 0.
  It scans the C# files under `tests/` with literals, enum members and names
  treated as interchangeable, so tests that differ only by data match.
- **AC-8**: Given two `[Fact]` tests whose bodies differ only by literals or
  enum members, as the original `AgentRoleTest` pair did, when the
  `duplicates` gate runs, then it fails. Its output names both files and line
  ranges. This applies to `AgentRoleTest.cs` as it stood before this change.
- **AC-9**: Given `tests/StyleCanary`, when `scripts/gates.sh verify` runs,
  then the canary contains such a pair of tests, tagged
  `// expect: duplicates:<rule>`, and the gate reports it. If the gate stopped
  reporting it, `verify` fails. This is the same mechanism the other gates use.
- **AC-10**: Given `tests/StyleCanary` is excluded from the real gates, when
  `scripts/gates.sh` runs without arguments, then the canary's duplicate pair
  does not fail the `duplicates` gate. `scripts/gates.sh` itself passes.
- **AC-11**: Given the `duplicates` gate, when its threshold (the minimum size
  of a match that counts) is chosen, then the value is recorded beside the
  setting with the reason. The value is low enough to flag a pair of tests of
  the size of the `AgentRoleTest` pair. It is high enough that the repository
  passes without weakening any other gate.
- **AC-12**: Given `docs/how-it-works/quality-gates.md`, when it is read, then
  the gates table lists `duplicates` with its tool and what it catches. Given
  `docs/contribute/coding-standards.md`, then the `[Theory]` bullet says the
  rule is enforced by the `duplicates` gate.
- **AC-13**: Given a `[Theory]` that has several rows, when the `duplicates`
  gate runs, then it does not report the rows as duplicates of each other.
  Parameterised tests are what the gate is steering authors toward.

## Out of scope

- The failed `git push` reported in the issue thread. It comes from the
  workflow's own branch handling, not from this item's behaviour.
- Duplicated production code under `src/`. Duplicated Python is already
  covered by pylint.
- Tests that differ in structure, such as in the Arrange or the Assert, and not
  only in their data.
- Changing `AgentRole`, `AgentTool` or any production behaviour.
- Mutation testing (Stryker.NET), which is a separate planned gate.

## Decisions

- **Enforcement uses a gate, not an analyzer or a review rule.** The
  repository's rule is that anything enforceable is a gate, and a rule that
  is only reviewed was how this slipped through. The tool should be a free,
  established clone finder. ReSharper's `dupfinder` is the natural choice,
  because the JetBrains command-line tools are already provisioned for
  `inspect`, and it can discard literals and members. The implementer may pick
  another free tool if `dupfinder` cannot do this, and must record why.
- **The gate scans `tests/` only.** The request is about tests, and widening
  it to `src/` could turn up unrelated findings.
- **The theory's name and the row mechanism are left to the implementer.**
  `AgentTool[]` is not a constant, so `[InlineData]` with a `params` array or
  `[MemberData]` may be needed.
- **Rows are the unit of fidelity (AC-6).** "Similar issues elsewhere" is
  defined by what the gate finds, so the fix list is testable and not a
  judgement call.
- **The threshold is not fixed here.** It cannot be known without running the
  tool (AC-11 sets the constraints).
- **The `duplicates` gate name and the canary tag format follow the existing
  gates.** `scripts/gates.sh verify` already requires every gate to have a
  canary.
