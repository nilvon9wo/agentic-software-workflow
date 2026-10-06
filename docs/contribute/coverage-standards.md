# Coverage Standards

## The framework must never make a consumer debug it

A test that fails because of an XFTY bug should say so loudly. So:

- **Any error that can trace back to the framework is loud** — a clear
  `XftyConfigurationException` naming the misconfigured field / relationship /
  call and the fix, never a silent `null` or an opaque downstream exception.
  Example: `context.SiblingValue(field)` throws, naming both fields and the
  `Put` order, rather than returning a misleading `null`
  ([../use/context-aware-values.md](../use/context-aware-values.md)).
- **Accessors that can miss throw at the call site.** `SharedAncestor.GetId`
  throws rather than returning `null` for an unresolved name.

---

## Line coverage is the floor; branch coverage is the goal

`coverlet.collector` measures line coverage; the real target is **branch**
coverage — every guard, every `switch`, every ternary, both sides — reviewed
by hand on every change, since automated tooling only gets you partway there.

- The line-coverage target is **~100%**, measured with
  `dotnet test --collect:"XPlat Code Coverage"` (see
  [local-development](local-development.md#measuring-coverage)).
- Remove dead code rather than covering it.

Scenarios still worth explicit tests as the engine grows: many-level graphs,
circular relationships beyond `PreventCascade`, and the open items in
[../reference/known-issues.md](../reference/known-issues.md).

---

## Doc examples: not yet mechanically enforced

Apex's `scripts/verify-doc-examples.py` checked every significant call in
every ` ```apex ` code block carrying a `Runnable:` marker against the test
class it named, in CI, on every push. **Porting that script to check
` ```csharp ` blocks against `Xfty.Test` is not done** — the docs currently
carry no `Runnable:` markers and no automated guarantee they stay in sync with
the code they describe. See
[porting-history](porting-history.md) for this as tracked,
open work. Until it exists, treat every code block in `docs/` as
believed-correct-but-unverified, and flag drift by hand when you notice it.
