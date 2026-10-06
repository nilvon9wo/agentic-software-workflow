## Summary
In `tests/AgenticSoftwareWorkflow.Conductor.Test/Agents/AgentRoleTest.cs`, the two tests `CanEdit_WhenGrantedEditFiles_IsTrue` and `CanEdit_WhenNotGrantedEditFiles_IsFalse` differ only in the tools passed to `AgentRole` and the expected `CanEdit` value. Replace them with one parameterised `[Theory]`, as coding-standards.md asks for data-row variations. This is a test-only refactor. It changes no production code and no behaviour.

## Acceptance criteria
1. Given `AgentRoleTest.cs`, when it is read after the change, then it has no `[Fact]` named `CanEdit_WhenGrantedEditFiles_IsTrue` or `CanEdit_WhenNotGrantedEditFiles_IsFalse`. A single `[Theory]` method covers `AgentRole.CanEdit`.
2. Given the theory, when it runs, then it has exactly two data rows:
   - tools `ReadFiles` and `EditFiles`, expected `true`;
   - tools `ReadFiles` and `SearchFiles`, expected `false`.
   Both rows use `CapabilityTier.Standard` and `AgentAccess.ToolsOnly`, as the existing tests do.
3. Given each row, when the theory runs, then it builds `new AgentRole(CapabilityTier.Standard, tools, AgentAccess.ToolsOnly)` and asserts that `CanEdit` equals the expected value. It uses `Assert.Equal`, or `Assert.True`/`Assert.False` chosen by the row. A bare boolean check is not allowed.
4. Given the theory body, when it is inspected, then it has the `// Arrange`, `// Act` and `// Assert` comments verbatim. The Act is a single statement (`bool canEdit = role.CanEdit;`). The test checks one behaviour.
5. Given the theory's name, when it is inspected, then it follows `<MethodUnderTest>_When<Condition>_<ExpectedOutcome>`, for example `CanEdit_WhenToolsVary_MatchesWhetherEditFilesIsGranted`. It has no `Test` prefix and no `Async` suffix.
6. Given the data source, when it is written, then it uses `[MemberData]` or `[ClassData]` on a typed source of `AgentTool[]` plus `bool`, because `[InlineData]` cannot hold the enum-array tool lists cleanly. Rows are not passed as strings that get parsed. Code follows the coding standards: `this.` on instance members, explicit types and no `var`.
7. Given the whole repository, when `scripts/gates.sh` runs, then it passes with 100% line and branch coverage. The test count for `AgentRole.CanEdit` is two cases, and both outcomes (true and false) are still exercised.

## Out of scope
- Changes to `AgentRole`, `AgentTool`, `AgentAccess` or any other production code.
- Adding new `CanEdit` cases, such as an empty tool list or other tiers or access modes.
- Refactoring other tests in the repository.

## Decisions
- Used `[MemberData]` or `[ClassData]` rather than `[InlineData]`, because the inputs are `AgentTool` collections and attribute arguments are poorly suited to them. The implementer picks whichever fits the repository's existing style.
- Kept exactly the two existing data rows, so behaviour coverage is unchanged.
- Suggested the theory name `CanEdit_WhenToolsVary_MatchesWhetherEditFilesIsGranted`. This is a suggestion only, and any name that follows the naming rule satisfies criterion 5.