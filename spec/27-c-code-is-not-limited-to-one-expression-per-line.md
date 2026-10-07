# Chained calls: one call per line in the coding standards

## Summary

`docs/contribute/coding-standards.md` says "One expression per line" but does
not say what that means for a chain of calls, so nothing defines or enforces
it. The maintainer has decided the rule: in a chain of calls, every call after
the first goes on its own line, indented one level past the start of the chain,
dot first. This item records that rule in the Formatting section of the coding
standards. Enforcement belongs to the Roslyn layout analyzer in #3 and is not
built here.

## Acceptance criteria

- **AC-1** Given `docs/contribute/coding-standards.md`, when its
  `## Formatting` section is read, then it contains a bullet that starts with
  the bold text `One expression per line` and states that in a chain of calls
  every call after the first goes on its own line, indented one level past the
  start of the chain, with the dot first.
- **AC-2** Given that bullet, when it is read, then it states that a member
  access before the first call is not split, and gives
  `this._processRunner.ReceivedCalls()` as the example of what stays together.
- **AC-3** Given that bullet, when it is read, then it contains a fenced
  `csharp` example showing `x.Foo()` followed on the next line by
  `.Bar()` indented four spaces further than `x`. It covers a chain of
  exactly two calls, so a single trailing call is shown to be split.
- **AC-4** Given that bullet, when it is read, then it states that dots are
  indented, not aligned, and why: alignment would need a custom formatter.
- **AC-5** Given the existing bullet `One expression per line; one variable
  declaration per line.`, when the document is edited, then the chain rule
  replaces or extends that bullet, so the document has no two bullets that
  state the one-expression-per-line rule separately or contradict each other.
- **AC-6** Given the edited document, when markdownlint runs on it, then it
  reports no problems: prose is wrapped at 80 columns, and the code block is
  fenced with backticks and names its language.
- **AC-7** Given the edited document, when `scripts/gates.sh` runs, then it
  passes. No source file, analyzer, or gate configuration changes in this
  item.
- **AC-8** Given the edited document, when it is read, then it does not claim
  the chain rule is currently enforced by a gate. If it mentions enforcement,
  it says the rule will be enforced by the layout analyzer tracked in #3.

## Out of scope

- Writing or changing any analyzer, gate, `.editorconfig` setting, or
  `tests/StyleCanary` violation. That is #3, which this item's enforcement
  depends on.
- Reformatting existing C# in `src/` or `tests/` that already breaks the rule.
  That is done when the analyzer lands.
- Changing the separate "Never more than one expression per line" item at
  line 27 of `coding-standards.md`, or the Python and shell standards.
- Aligning dots. The maintainer rejected it.

## Decisions

- The change is documentation only. The maintainer's last reply says
  enforcement is #3's job with its own canary, so adding a gate here would
  duplicate that work.
- The example is typed into the Markdown as a `csharp` fence. `CLAUDE.md` says
  code in documentation comes from tests via `mdsnippets`. The existing
  "wrapped call" example in the same section is also hand-typed, and a
  two-line layout fragment is not a compilable, testable snippet. The example
  follows that precedent. If a maintainer wants it sourced from a test, that is
  a separate change.
- The rule goes in the existing Formatting bullet instead of a new section,
  because the maintainer's wording names it "One expression per line", which is
  already that bullet's title (AC-5).
- AC-8 is included because the analyzer does not exist yet. The repository has
  no layout analyzer, only the `layout` gate for line length and closing
  parentheses. Documenting the rule as enforced would be false until #3
  lands.
