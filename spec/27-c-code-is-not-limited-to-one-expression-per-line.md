# One non-trivial evaluation per line in the coding standards

## Summary

`docs/contribute/coding-standards.md` says "One expression per line" in two
places (house rule 4 and the Formatting section) but never defines what counts
as an expression, so nothing defines or enforces it. The maintainer has decided
the general rule: each line evaluates at most one non-trivial thing. A short
list of constructs is free and does not count, dots used for plain member
access do not count unless the chain is long, and null-conditional,
null-coalescing, and ternary operators are always broken across lines in a
defined layout. This item replaces the narrow chained-call wording in the
coding standards with that rule, for every language the repository holds to the
standards. Enforcement belongs to the Roslyn layout analyzer in #3 (and
equivalent checkers for other languages) and is not built here. This
specification replaces the earlier chain-only specification for #27.

## Acceptance criteria

- **AC-1** Given `docs/contribute/coding-standards.md`, when its
  `## Formatting` section is read, then it contains exactly one bullet that
  starts with the bold text `One non-trivial evaluation per line` and states
  that a line evaluates at most one non-trivial expression or statement, and
  that a declaration still takes one variable per line.
- **AC-2** Given that bullet, when it is read, then it lists the free
  constructs that are not counted, naming each of: `await`, casts, literals,
  names, unary `!` and `-`, `not`, `nameof()`, `typeof()`, and short property
  access.
- **AC-3** Given that bullet, when it is read, then it states that dots used
  for plain member access are not counted, so `this._processRunner.ReceivedCalls()`
  stays on one line, but each call in a chain of calls after the first goes on
  its own line, dot first, indented one level past the start of the chain. It
  shows a `csharp` example with `x.Foo()` followed on the next line by `.Bar()`
  indented four spaces further than `x`, so a chain of exactly two calls is
  shown to be split.
- **AC-4** Given that bullet, when it is read, then it states that a member
  access chain with three or more consecutive member accesses and no call is
  "long" and is also broken, one `.member` per line, dot first, indented one
  level past the start of the chain.
- **AC-5** Given that bullet, when it is read, then it states that a
  null-conditional (`?.`) or null-coalescing (`??`) operator is always placed
  at the start of its own line, indented one level past the start of the
  statement, and it shows a `csharp` example equivalent to the following.

  ```csharp
  string a = foo
      ?.bar
      ?.baz
      ?? "whatever";
  ```

- **AC-6** Given that bullet, when it is read, then it states that a ternary
  is broken into three lines, condition then `? whenTrue` then `: whenFalse`,
  with `?` and `:` first on their lines and indented one level past the
  condition. It states that a ternary nested in the false branch indents its
  own `?` and `:` one further level than the enclosing ones. It shows a
  `csharp` example equivalent to the following.

  ```csharp
  string b = isFoo()
      ? "A"
      : isBar()
          ? "B"
          : "C";
  ```

- **AC-7** Given that bullet, when it is read, then it states that dots are
  indented, not aligned, and why: alignment would need a custom formatter.
- **AC-8** Given that bullet, when it is read, then it states that the rule
  applies in every language the repository holds to these standards, with
  each language's own syntax for the same constructs.
- **AC-9** Given the edited document, when house rule 4 ("Never more than one
  expression per line.") and the "one expression per line" sentence in the
  cross-language section are read, then each is reworded to refer to the new
  bullet, and no two places state the rule separately or contradict each
  other. The old bullet `One expression per line; one variable declaration per
  line.` no longer exists.
- **AC-10** Given the edited document, when it is read, then it does not claim
  the rule is currently enforced by a gate. If it mentions enforcement, it says
  the rule will be enforced by the layout analyzer tracked in #3.
- **AC-11** Given the edited document, when markdownlint runs on it, then it
  reports no problems: prose is wrapped at 80 columns, and every code block is
  fenced with backticks and names its language.
- **AC-12** Given the edited document, when `scripts/gates.sh` runs, then it
  passes. No source file, analyzer, or gate configuration changes in this
  item.

## Out of scope

- Writing or changing any analyzer, gate, `.editorconfig` setting, or
  `tests/StyleCanary` violation. That is #3 for C#, and separate work for
  other languages.
- Reformatting existing code in `src/`, `tests/`, or `scripts/` that already
  breaks the rule. That is done when enforcement lands.
- Aligning dots. The maintainer rejected it.
- Changing the nesting rule (house rule 6), the boolean-chain rule (house rule
  7), or the wrapped-call closing-parenthesis rule.

## Decisions

- The change is documentation only. The maintainer says enforcement is #3's
  job with its own canary, so adding a gate here would duplicate that work.
- The examples are typed as `csharp` fences, as the existing examples in the
  same document are. `CLAUDE.md` says documentation code comes from tests via
  `mdsnippets`, but these are layout fragments that cannot compile. If a
  maintainer wants them sourced from tests, that is a separate change.
- "Long" access chain is defined as three or more consecutive member accesses
  with no call (AC-4). The maintainer said only "very long" and gave no number.
  Three keeps `this._processRunner.ReceivedCalls()` and
  `this._field.Property` whole. A maintainer can change the number in review.
- "Short property access" is taken to mean a chain of at most two member
  accesses. It follows from the long-chain threshold, so the two definitions
  do not overlap.
- A call counts as a non-trivial evaluation, and a plain member access does
  not. This reconciles "dots are not counted" with the earlier decision that
  `x.Foo().Bar()` is split: the split follows from two calls, not from the
  dots.
- `?.` and `??` are always broken, even in short chains, because the maintainer
  said "especially if there are null-coalesce operators". A `?.` with no
  other evaluation is read as part of the same rule.
- The indent is four spaces, one level, as in the existing standards. The
  maintainer's examples used wider, uneven indents. The wording is "one
  level", with nested ternaries one level further, because exact columns
  would need an aligner, which was rejected.
- The cross-language bullet in the document is reworded rather than
  duplicated (AC-9), so other languages inherit the rule without a copy of it.
- AC-10 is included because no analyzer exists yet. The repository has only
  the `layout` gate for line length and closing parentheses, so documenting the
  rule as enforced would be false until #3 lands.
