# One expression per line: call chains

## Summary

Add a house rule, "One expression per line", to `docs/contribute/coding-standards.md`, and enforce it with a Roslyn layout analyzer in a build-time gate. In a chain of calls, every call after the first goes on its own line, indented one level (4 spaces) past the start of the chain, dot first. A member access before the first call is not split. The rule is currently only reviewed, and `docs/how-it-works/quality-gates.md` lists it under "Known gaps". This item moves it to enforced and brings existing C# into compliance. It **depends on #3** (the Roslyn layout analyzer): this item adds one rule to the analyzer project that #3 creates and cannot be built before #3 merges.

Definitions used below:
- A **chain** is an expression made of two or more invocations linked by `.` or `?.` member access. For example, `a.b.C().D()` has the receiver `a.b`, the first call `C()` and the subsequent call `D()`.
- The **first call** is the first invocation in the chain. Everything before it (the receiver and member accesses such as `this._processRunner.`) stays on the line where the chain starts, together with the first call.
- A **subsequent call** is every later invocation. Any element access (`[0]`), `!`, or non-invocation member access that directly follows a call stays on that call's line.

## Acceptance criteria

1. Given the standard text is edited, when `docs/contribute/coding-standards.md` is read, then the house rules contain an "One expression per line" rule that states the chain rule: each call after the first goes on its own line, indented one level, dot first. It states that a member access before the first call is not split, and that dots are indented, not aligned.
2. Given the standards are edited, when the rule's code example is inspected, then the C# comes from a test via `mdsnippets` and is not typed by hand into the Markdown.
3. Given `docs/how-it-works/quality-gates.md`, when it is read after this change, then "one expression per line" is no longer listed among the house rules that are reviewed rather than enforced, and the rule is described as enforced by the layout analyzer.
4. Given C# source containing `x.Foo().Bar();` on one line, when the solution is built, then the analyzer reports the chain-layout diagnostic at severity error on `.Bar()` and the build fails. The same call formatted as `x.Foo()` on one line and `.Bar()` on the next, indented 4 spaces past the start of the statement, produces no diagnostic.
5. Given a chain with three or more calls, such as `this._processRunner.ReceivedCalls().Single().GetArguments()[0]!`, when it is written with `ReceivedCalls()` on the first line and `.Single()` and `.GetArguments()[0]!` each on their own following line, then no diagnostic is reported. When `.Single()` and `.GetArguments()` share a line, or `.Single()` sits on the first line, then a diagnostic is reported for each call that is on a line it should not be on.
6. Given a chain with exactly one call, such as `this._processRunner.ReceivedCalls()`, `new Foo().Bar()`, or `a.b.c.Method()`, when it is on a single line, then no diagnostic is reported. In `new Foo().Bar()` the object creation is the receiver, so `.Bar()` is the first and only call.
7. Given a split chain whose subsequent-call lines are indented by anything other than exactly one indentation level (4 spaces) past the line where the chain starts, when the solution is built, then the chain-layout diagnostic is reported. Aligning the dots under the first dot is such a violation.
8. Given a split chain where a line begins with the call and the dot ends the previous line (for example `x.Foo().` followed by `Bar()`), when the solution is built, then the diagnostic is reported. The dot must come first on the continuation line.
9. Given a chain using null-conditional access (`x.Foo()?.Bar()`), when `?.Bar()` is on its own line starting with `?.`, then no diagnostic is reported. When it shares the line of the preceding call, then a diagnostic is reported.
10. Given a chain with generic calls (`x.Foo<T>().Bar<U>()`) or calls with arguments spanning several lines, when each subsequent call starts on its own line, one level in, then no diagnostic is reported. The existing rule that a wrapped call closes with `)` on its own line still applies independently.
11. Given a chain nested inside another call's arguments or inside a lambda, when it is checked, then it is judged on its own: its indentation is relative to the line on which that inner chain starts, and it is not exempt because it is nested.
12. Given LINQ chains, when they have two or more calls, then they are subject to the same rule, and there is no exemption.
13. Given the canary, when `scripts/gates.sh verify` runs, then `tests/StyleCanary/Violations.cs` contains a violation of this rule tagged with the gate and rule that must catch it, using the existing `// expect: <gate>:<rule>` convention. `verify` fails if the analyzer stops reporting it.
14. Given the whole repository after this change, when `scripts/gates.sh` runs, then it passes. Every existing chain in `src/` and `tests/` that violates the rule, including the `SentRequest()` example from the issue, has been reformatted. No gate, threshold, or rule has been weakened and no suppression has been added to achieve this.
15. Given analyzer unit tests, when they run, then there is one test per criterion 4–12, and the analyzer project keeps 100% line and branch coverage.

## Out of scope

- Aligning dots under the first dot. This would need a custom formatter and was rejected.
- Rules for Python, shell, or Markdown chains.
- Other "one expression per line" cases that are not call chains, such as multiple statements per line or boolean chains. Boolean chains already have their own rule.
- Creating the Roslyn layout analyzer project itself, which is #3.
- Replacing the lexical `layout` gate for line length and `)` placement.

## Decisions

- **Dependency on #3.** The analyzer project does not exist yet, so this item builds on #3 as the maintainer's recorded decision says. The diagnostic takes the next free ID in whatever scheme #3 establishes, and the tests refer to it by that ID.
- **Chain threshold.** A chain is split only when it has two or more invocations. `x.Foo().Bar()` has two, so it is split, as the maintainer confirmed. A single call is never split.
- **Non-call members.** Property access, element access, and `!` that directly follow a call stay on that call's line, so `.GetArguments()[0]!` is one unit. This follows from the issue's example and the rule that only calls after the first are split.
- **Null-conditional `?.`.** Treated like `.`: the continuation line starts with `?.`.
- **Object creation as receiver.** `new Foo().Bar()` counts as one call, so it is not split.
- **Severity.** The diagnostic is an error, like the other enforced house rules, so it fails the build rather than only warning.
- **Existing code.** It is reformatted in this item rather than suppressed, because the gates must pass and suppressions are forbidden without a reason.
- **Rule wording.** It is taken from the maintainer's recorded decision on this issue: each call after the first on its own line, one level of indentation, dot first, no alignment.
