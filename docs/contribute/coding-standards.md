# Coding Standards

The rules this port's code is held to. They apply to anyone changing this
repo — human or AI. When a change is reviewed, this is the checklist.

**The authoritative style rules:**

- **`.editorconfig`**, at the repo root. Every rule in it is `severity = error`
  on purpose — see [Everything is `error`](#everything-is-error) below.
  `EnforceCodeStyleInBuild=true`, so `dotnet build` fails on most of them;
  the two it does not run (IDE1006 naming, IDE0130 namespace-folder) are caught
  by `dotnet format` in CI — see [Quality gates](#quality-gates). Notable rules:
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
  5. Blocks must not be nested more than two deep.
  6. Never nest expressions. The accepted ceiling is one `Field.Of(...)` inside
     one call (as in `RecordProvider.FieldConfigLambda`); anything deeper gets
     a named intermediate.
  7. Use variables or methods to name the results of all complex expressions.
  8. All classes, methods, and variables must be named to communicate
     intentions — never a single letter or abbreviation.
  9. Always use nouns to name objects.
  10. Always use verbs to name methods.
  11. Always use adjectives to name interfaces.
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
design principles carried over from the Apex original, and this port's own
testing conventions.

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
|---|---|
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

`dotnet build` does **not** run the naming analyzer (IDE1006) even with
`EnforceCodeStyleInBuild`; nor IDE0130 (namespace matches folder), which only
fires for `partial` types split across files. `dotnet format` in CI is the gate
for both — see [Quality gates](#quality-gates).

---

## Formatting

- **Line length: 80 soft, 120 hard.** Never over 120 (`.editorconfig`
  `max_line_length = 120`; CI checks with `awk 'length>120'`). There is
  essentially always a clearer way to express a line that long.
- **One expression per line; one variable declaration per line.**
- **Long strings** are broken and `+`-concatenated across lines, never left to
  overflow.
- **A wrapped call or declaration closes with `)` on its own line**, aligned to
  the statement that opened it — symmetric with the opener, never dangling after
  the last argument. (`.editorconfig`
  `csharp_wrap_before_invocation_rpar` / `_declaration_rpar`, plus the
  ReSharper equivalents.)

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
   its own contract, its own tests? Extract it. (`MasterTemplate.Copy` became a
   copy constructor; `RecordProvider`'s execution pipeline became
   `RecordProviderPlan` + `RecordProviderExecution`.)
2. **Is the surface irreducible** — a fluent builder, a forwarding wrapper, a
   visitor, or a set of overloads that must stay consistent with a sibling
   type's? Keep it in one file, or split into concern-partials **whose file and
   type names carry the split** so a developer navigates by them with no
   reliance on doc comments or collapsed regions. Accept the length.
3. Only if 1 and 2 both fail and the file is genuinely unwieldy do you reach for
   concern-partials as a last resort.

---

## Quality gates

CI (`.github/workflows/ci.yml`) fails the build on any of:

| Gate | Covers |
|---|---|
| `dotnet build` (`EnforceCodeStyleInBuild`) | compilation; every `.editorconfig` analyzer that runs in-build (IDE00xx, CA1xxx, …) |
| `dotnet format Xfty.slnx --verify-no-changes --severity info` | whitespace/formatting; **IDE1006 naming and IDE0130 namespace-folder**, which the build does not run |
| `dotnet test` (cross-platform slnf) | the full suite, all TFMs |
| `windows-net472` job | the netstandard2.0 build actually runs (net472) |
| `verify-doc-examples.py` / `verify-doc-links.py` | every documented code call is exercised by a test; every relative doc link resolves |

Run the same `dotnet format` check locally before pushing — see
[local-development](local-development.md).

---

## Design

- **Polymorphism over branching.** A `null`/type check that the same code
  makes in more than one place is a missing type. Introduce a strategy
  interface with one implementation per case; the caller stops choosing. A
  wall of near-identical `if (bad) throw` guards collapses the same way — one
  `Assert...`/reject helper, one line per rule.
- **Flyweight whenever possible.** Interned instances obtained through a
  `Get(...)` factory (see `LookupKey.Get`, `SharedAncestor.Get`), never `new`.
- **Explicit over stateful.** Reject registry / mutable-builder APIs where a
  complete, explicit `Dictionary` plus a stateless static class will do (see
  `ProviderLookups`). Where a collaborator needs values it does not yet all
  have, pseudo-closure the ones it has via the constructor; where it needs many
  things at once, a fluent builder is acceptable.
- **Immutability.** Clone aggressively (`RecordCloneFactory`); derive a new
  object rather than mutating (`MasterTemplate.Copy()`, `GenerationContext`'s
  `With*` methods).
- Remove dead code rather than working around it.
- **No nested classes, ever.** A private helper scoped to one file (a test
  double, a small worker class) is `file sealed class Foo` at namespace scope
  in the same `.cs` file — C#'s direct equivalent of Apex's private-inner-
  class-scoped-to-one-`.cls`-file pattern.

---

## C# gotchas specific to this port

- **`init`-only properties** are read-only after construction by design
  (compile-time only) — `PropertyInfo.SetValue` bypasses that restriction via
  reflection, which `IdMocker`, `RecordCloneFactory`, and `RecordInjector`
  all rely on deliberately. Don't "fix" a reflection-based writer to respect
  `init` — that would break the mechanism.
- **`PropertyInfo` equality** across two `Field.Of<T>(nameof(...))` calls for
  the same property is reference-equal (reflection caches `PropertyInfo`
  instances per type), so it works as a dictionary key without a custom
  comparer — but a `PropertyInfo` obtained through a *different* route
  (e.g. `GetType().GetProperty(...)` vs. `typeof(T).GetProperty(...)`) may not
  be, so this port is consistent about always going through `Field.Of<T>`.
- **`static` state does not reset between xUnit test methods** the way it did
  between Apex test methods — see
  [reference/salesforce-considerations](../reference/salesforce-considerations.md).
  This is the single most important behavioral difference to keep in mind
  while writing tests, and it has caused real cross-test-contamination bugs
  during this port's development (see the fixed-defects list in
  [porting-history](porting-history.md)).

---

## Testing and coverage

- **Line coverage ~100%**, measured with `coverlet.collector` (see
  [local-development](local-development.md#measuring-coverage)).
- **Branch coverage is the real goal** — every guard, `switch`, and ternary,
  both sides, checked by hand.
- **The framework must never make a consumer debug it.** Any error that could
  trace back to XFTY is loud: a clear `XftyConfigurationException` naming the
  misconfiguration and the fix — never a silent `null` or an opaque downstream
  exception. Accessors that can miss throw at the call site.
- **One test class per unit under test**, sitting beside it under the mirrored
  folder structure in `Xfty.Test/` — `Xfty/Core/Bundle.cs` → `Xfty.Test/Core/BundleTest.cs`.
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
  test assumes) — carried over unchanged from the Apex original's convention.
- **Names:** `<MethodUnderTest>_When<Condition>_<ExpectedOutcome>` — PascalCase,
  no `Test` prefix — e.g. `IsSatisfiedBy_WhenTheFieldIsBlank_ReturnsFalse`,
  `Of_WhenTheListIsNull_Throws`. For an end-to-end / scenario test,
  `<MethodUnderTest>` is the entry point exercised (`Supply_…`,
  `SupplyBundle_…`, `Flush_…`).
- **`[Theory]` for data-row variations** where xUnit's parameterisation fits;
  otherwise a thin `[Fact]` calling a shared private runner that holds the
  `// Arrange` / `// Act` / `// Assert` is the direct equivalent of the Apex
  pattern of one data-row test method per case plus a shared runner.
- **`Assert.*`, never a bare boolean check standing in for one.** Expecting a
  throw: `Assert.Throws<TheSpecificException>(() => act())` — the *exact*
  type, never a bare `Exception`.
- **Test doubles are code too.** Don't paste near-identical
  `IRecordProvider`/`IProviderLookup` implementations across test files — a
  `file sealed class` fixture per file is fine, but reuse a shared helper
  method for anything reused within one file.
