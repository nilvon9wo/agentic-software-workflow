# Coding Standards

The rules this project's code is held to. They apply to anyone changing this
repo — human or AI. When a change is reviewed, this is the checklist.

**The authoritative style rules:**

- **`.editorconfig`**, at the repo root. Every rule in it is `severity = error`
  on purpose — see [Everything is `error`](#everything-is-error) below.
  `EnforceCodeStyleInBuild=true`, so `dotnet build` fails on most of them;
  what the build cannot check is covered by the other
  [quality gates](../how-it-works/quality-gates.md). Notable rules:
  IDE0058 (discard unused fluent-return values with `_ =`), IDE0032 (where a
  private field is exposed by a *trivial* public property, collapse the pair to
  a public auto-property), IDE0022 (expression-bodied members where possible),
  IDE0090 (`new()` target-typed construction), the naming rules
  ([Naming](#naming)), and the 80/120 line ceiling and wrapping rules
  ([Formatting](#formatting)).
- **This project's own house rules**, layered on top of the analyzers above
  (not separately tool-enforced — reviewed by hand on every change):
  1. Prefer functional over imperative. A `foreach` is acceptable where the
     alternative is a contorted LINQ chain or a `.ToList().ForEach(...)`; a
     bare `for`/`while` counter almost never is.
  2. Prefer explicit over implicit.
  3. Keep methods short — a method past ~10 lines is usually doing two things.
     This is a smell to investigate, not a hard limit to contort code around.
  4. Never more than one expression per line.
  5. Blocks must not be nested more than two deep — except that a
     `try`/`catch` may be a third layer. A `try` wraps only the code expected
     to throw, and every `catch` names a specific exception type (CA1031 in
     C#; ruff's BLE001/E722 in Python). Where the project allows it, prefer a
     monadic result over `try`/`catch` altogether — see
     [Errors as values](#errors-as-values).
  6. Never nest expressions. The accepted ceiling is one simple call inside
     one call; anything deeper gets a named intermediate.
  7. Use variables or methods to name the results of all complex expressions
     — when the result *means* something. A name communicates intent; never
     introduce one just to shorten or break up a line. Two booleans may share
     a line (`foo && bar`); a chain of three or more puts each operand on its
     own line, operator first:

     ```csharp
     return foo
         && bar
         && bat;
     ```
  8. All classes, methods, and variables must be named to communicate
     intentions — never a single letter or abbreviation.
  9. Always use nouns to name objects.
  10. Always use verbs to name methods — and never suffix them with `Async`.
      The suffix repeats what `async`, `await`, and the `Task` return type
      already say. What it was meant to guard against, a forgotten `await`, is
      a build error instead (CS4014 and VSTHRD110). Framework methods keep
      their own names (`ReadAllTextAsync`).
  11. Always use adjectives to name interfaces, prefixed with `I` as C#
      convention requires — `IDisposable`, `IAgentic`, `IProcessCapable`. An
      interface describes what its implementers *are able to do*; nouns name
      the classes that do it.
  12. Always name booleans like `isSomething`, `wasSomething`, `hasSomething`,
      etc.
  13. Always use the keyword `this`, except to reference static members.
  14. Always extract magic numbers and strings to constants.
  15. Prefer ternary to if/then, but never squeeze the condition and both
      outputs onto one line — three lines.
  16. Don't nest a complicated expression inside a ternary: extract it to a
      variable or method first.
  17. **Intention and separation of concerns over line count.** See
      [Class size and `partial`](#class-size-and-partial).
  18. Never use inner classes — see [No nested classes, ever](#design) below
      for the `file`-scoped-class replacement.
  19. Avoid circular dependencies.

This page covers what those two don't: naming, formatting, class structure,
design principles, testing conventions, and the equivalent rules for the other
languages in the repository.

---

## Everything is `error`

Every diagnostic in `.editorconfig` is `severity = error`, deliberately. A
diagnostic is *fixed*, never demoted to `warning` or `suggestion` to sit on a
growing list that then gets ignored. There is no "we'll get to it" tier.

If a rule genuinely cannot be satisfied at one site (a `file`-local type in a
public signature that CA1859 wants concrete, say), restructure the code so the
rule is satisfied — inline the helper, change the seam — rather than suppress.
A `#pragma`/`[SuppressMessage]` is a last resort, carries an inline comment
saying why, and is called out in the commit message for review.

---

## Naming

Standard modern C# / .NET-runtime style: `PascalCase` everywhere, except where a
convention says otherwise.

| Symbol | Style |
| --- | --- |
| types, namespaces, methods, non-private properties & events, type parameters (`TRecord`) | `PascalCase` |
| interfaces | `IPascalCase` |
| `const` | `PascalCase` |
| `static readonly` values (constant-shaped data, flyweight caches) | `PascalCase` |
| private instance fields — including `readonly` backing state, as **fields**, not `{ get; }` properties | `_camelCase` |
| genuinely-mutable private static fields | `s_camelCase` |
| private properties | `PascalCase` |
| parameters (including primary-constructor parameters), locals, lambda parameters | `camelCase` |

Backing state is a `private readonly Foo _foo;` **field**, not a
`private Foo Foo { get; }` property — a property implies behaviour it doesn't
have. The exception is IDE0032's own fix: where a *trivial* public property
already exposes the field (`public string Name => this._name;`), collapse the
pair to `public string Name { get; }`.

Naming (IDE1006) is enforced by `dotnet build` — verified by the
[canary](../how-it-works/quality-gates.md#proving-the-gates-work).

---

## Formatting

- **Line length: 80 soft, 120 hard.** Never over 120 (`.editorconfig`
  `max_line_length = 120`; Roslyn ignores it, so the `layout` gate enforces it). There is
  essentially always a clearer way to express a line that long.
- **One expression per line; one variable declaration per line.**
- **Long strings** are broken and `+`-concatenated across lines, never left to
  overflow.
- **A wrapped call or declaration closes with `)` on its own line**, aligned to
  the statement that opened it — symmetric with the opener, never dangling after
  the last argument. (`.editorconfig`
  `csharp_wrap_before_invocation_rpar` / `_declaration_rpar`, plus the
  ReSharper equivalents; enforced by the `layout` gate.)

  ```csharp
  // no
  return new GenerationContext(
      providerLookup, insertMode, inclusivity, gateway, filler);

  // yes
  return new GenerationContext(
      providerLookup,
      insertMode,
      inclusivity,
      gateway,
      filler
  );
  ```

- **`this` for every instance member**, static members unqualified
  (`.editorconfig` `dotnet_style_qualification_for_*`).

---

## Class size and `partial`

Rule 17 used to read "classes must not exceed 100 lines." It doesn't. The real
smell starts around **~250 lines**, and even then the question is *intention and
separation of concerns*, not the count. A cohesive type with a genuinely large
surface (a fluent builder, a forwarding wrapper) is fine at 300 lines in one
file; a 120-line class doing two unrelated jobs is not.

`partial` divides *text*, not *concerns* — a yellow flag, not a first resort.
Decision order for a class that is getting large:

1. **Is there a real collaborator to extract** — something with its own name,
   its own contract, its own tests? Extract it.
2. **Is the surface irreducible** — a fluent builder, a forwarding wrapper, a
   visitor, or a set of overloads that must stay consistent with a sibling
   type's? Keep it in one file, or split into concern-partials **whose file and
   type names carry the split** so a developer navigates by them with no
   reliance on doc comments or collapsed regions. Accept the length.
3. Only if 1 and 2 both fail and the file is genuinely unwieldy do you reach for
   concern-partials as a last resort.

---

## Quality gates

Every rule that a tool can check is checked by a gate, and every gate is
proven to fire by the canary. See
[quality gates](../how-it-works/quality-gates.md) for the list, and
[local development](local-development.md) for running them.

---

## Design

- **Polymorphism over branching.** A `null`/type check that the same code
  makes in more than one place is a missing type. Introduce a strategy
  interface with one implementation per case; the caller stops choosing. A
  wall of near-identical `if (bad) throw` guards collapses the same way — one
  `Assert...`/reject helper, one line per rule.
- **Flyweight whenever possible.** Interned instances obtained through a
  `Get(...)` factory, never `new`.
- **Explicit over stateful.** Reject registry / mutable-builder APIs where a
  complete, explicit `Dictionary` plus a stateless static class will do.
  Where a collaborator needs values it does not yet all
  have, pseudo-closure the ones it has via the constructor; where it needs many
  things at once, a fluent builder is acceptable.
- **Immutability.** Derive a new object rather than mutating — as
  `ClaudeInvocation`'s `With*` methods do.
- **Dead code is removed — once you know why it is dead.** Code that looks
  unused may be used from outside the repository: the public API of a library
  (Xfty's whole surface is consumed by other projects), reflection, or a
  framework convention. Find out first. Code that is deliberately used only
  from outside is marked, so tools and reviewers can tell it from dead code,
  using [JetBrains.Annotations](https://www.nuget.org/packages/JetBrains.Annotations)
  (which the `inspect` gate honours):
  - `[PublicAPI]` — part of a library's surface, called by *other projects*.
  - `[UsedImplicitly]` — called by *machinery* rather than by code: reflection,
    serialisers, dependency injection, test frameworks, naming conventions.

  Both are used wherever they apply, including in code AI workers generate.
  A published library's public surface is additionally tracked in
  `PublicAPI.Shipped.txt` via
  [Microsoft.CodeAnalysis.PublicApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers).
  Code that is truly dead is deleted rather than covered or worked around.
- **No `ConfigureAwait` noise.** `ConfigureAwait(false)` only matters where a
  `SynchronizationContext` exists (UI frameworks, legacy ASP.NET). This
  project's code runs in console processes, where it does nothing — so it is
  not written. A library destined for hosts that do have one states the
  intent once, as an assembly-level attribute via
  [ConfigureAwait.Fody](https://github.com/Fody/ConfigureAwait), rather than
  at every `await`.
- **No nested classes, ever.** A private helper scoped to one file (a test
  double, a small worker class) is `file sealed class Foo` at namespace scope
  in the same `.cs` file.

---

## Testing and coverage

- **100% line and branch coverage**, enforced by every `dotnet test` — see
  [coverage standards](coverage-standards.md).
- **Errors as values.** <a id="errors-as-values"></a>In this project, expected
  failures are values, not exceptions: a monadic `Try`/`Fin`/`Either` from
  [LanguageExt](https://github.com/louthy/language-ext) makes the failure
  path part of the type and composes without nested `try`/`catch`.
  Exceptions remain for the genuinely exceptional — bugs and
  misconfiguration. (Projects this workflow builds may choose differently;
  this is a per-project decision, recorded in that project's standards.)
- **Failures are types, not codes.** Each kind of expected failure is its
  own type (deriving from `ExpectedFailure`) carrying its own data —
  `AgentTimedOut(Timeout)`, `CommandFailed(Command, ExitCode, StandardError)`.
  A numeric error code exists only to be switched on, which is branching
  logic in disguise; responses to failures are chosen polymorphically.
- **Errors are loud.** A misconfiguration fails at the call site with an
  exception naming the problem and the fix — never a silent `null` or an
  opaque downstream exception.
- **One test class per unit under test**, sitting beside it under the mirrored
  folder structure under `tests/` — `src/X/Claude/ClaudeInvocation.cs` →
  `tests/X.Test/Claude/ClaudeInvocationTest.cs`.
  Split a class that mixes fundamentally different scenarios (e.g. a fluent-API
  affordance test vs. an end-to-end scenario test).
- **One behaviour per test method.** A positive and a negative case are two
  behaviours — two methods. Every assertion must be about the single value
  captured in the Act; an assertion that re-invokes the code under test (with
  other inputs) is a second Act in disguise.
- **The Act is exactly one statement.** Declare the result variable in Arrange,
  assign it in Act, read it in Assert. Nothing acts in Assert.
- **AAA comments, verbatim**: `// Arrange`, `// Act`, `// Assert`, and
  `// Sanity Check` (a pre-Act assertion that the arranged state is what the
  test assumes). Expecting a throw, the Act captures the exception
  (`ArgumentException thrown = Assert.Throws<ArgumentException>(() => ...);`)
  and the Assert checks it — e.g. its `ParamName`, which proves *which* guard
  fired.
- **Names:** `<MethodUnderTest>_When<Condition>_<ExpectedOutcome>` — PascalCase,
  no `Test` prefix — e.g. `WithTools_WhenGivenNoToolNames_Throws`. For an
  end-to-end / scenario test, `<MethodUnderTest>` is the entry point exercised.
- **`[Theory]` for data-row variations** where xUnit's parameterisation fits;
  otherwise a thin `[Fact]` per case calling a shared private runner that holds
  the `// Arrange` / `// Act` / `// Assert`.
- **`Assert.*`, never a bare boolean check standing in for one.** Expecting a
  throw: `Assert.Throws<TheSpecificException>(() => act())` — the *exact*
  type, never a bare `Exception`.
- **Test doubles are code too.** Prefer NSubstitute to hand-written doubles;
  where a hand-written one is clearer, it is a `file sealed class` in the test
  file, and anything reused within one file is a shared helper method.
- **Test data**: use [Xfty](https://github.com/nilvon9wo/ExtremeCSharpTestDataFactory)
  where tests need populated records rather than hand-building them. Problems
  found with Xfty are reported upstream, not worked around.

---

## Other languages

Every language in this repository — and in projects this workflow builds — is
held to standards equivalent to these, adapted to its own conventions. Each
gets the same layers C# has: layout and whitespace rules, a linter, a type
checker where the language has one, and a canary proving each gate fires.

| Language | Layout / whitespace | Linter / types | Configuration |
| --- | --- | --- | --- |
| Python | ruff's pycodestyle rules + house-rule checkers | `ruff` (every rule enabled), `pylint` + house-rule checkers, `pyright` strict; `pytest` at 100% coverage | `pyproject.toml` |
| Shell | — | `shellcheck` | — |
| Markdown | `markdownlint` | `markdownlint`, `lychee` (links) | `.markdownlint-cli2.jsonc` |
| GitHub workflows | — | `actionlint` | — |

The house rules above carry over where they make sense in another language:
intention-revealing names, short single-purpose functions, no magic values,
explicit over implicit, one expression per line, and every suppression
justified beside it. Naming follows each language's convention — `snake_case`
functions in Python, for example.

### Python specifics

- **No conditional expressions** (`a if condition else b`). Python's version
  reads condition-in-the-middle and hides a branch inside an expression. Write
  an `if`/`else` statement, with **both branches written out**, even when the
  first returns — the rules that demand dropping the `else` are disabled.
- **Never nest expressions.** One call inside one call is the ceiling, and a
  comprehension counts as a level; name the inner result instead.
- **Blocks nest at most two deep**, or three when one is a `try`.
- **80 columns, hard.**
- **Boolean chains:** two booleans may share a line; three or more put each
  operand on its own line, operator first (E9004):

  ```python
  return (
      foo
      and bar
      and bat
  )
  ```

- **Wrapped calls, signatures, and collections** put each item on its own
  line, with a trailing comma and the closing bracket alone on its line —
  the C# wrapping rule (E9005, COM812).
- **Wrapped comprehensions** put the element and each `for` and `if` clause
  on its own line (E9006).
- **No formatter.** A formatter imposes its own layout — `ruff format` folds
  a split boolean chain back onto one line, for example — so layout is
  checked by lint rules and fixed by hand (or by the AI worker), as with C#.
- **Split modules by intent, never with comment banners.** As with C#
  classes, a module doing several jobs becomes several modules whose names
  say what each does (see `scripts/gates/`).
- Complexity at most 5, at most 5 branches and 3 returns per function, and no
  name shorter than three characters.

ruff and pyright cover most of this. The rest — conditional expressions,
nested calls, the try-aware nesting rule, and the three layout rules — is
enforced by custom pylint checkers in [`scripts/lint/`](../../scripts/lint/house_rules.py),
each with tests and a canary.
