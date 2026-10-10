# Free-tier AI providers as advisory, per-project, fallback-chained agents

## Summary

The conductor can run only on Claude, so the loop spends the whole Claude
allowance. This item adds the machinery to let other AI supplement it, without
trusting that AI with decisions. It adds an optional `agents` section to
`aswf.json` that names providers (Gemini and Groq, both free-tier HTTP APIs)
and, per role, an ordered fallback chain of them. A new `IAgentic` decorator
walks a role's chain: a rate-limited or failing provider is skipped to the
next, and a chain either ends in Claude or fails loudly. Non-Claude providers
may only run advisory roles (no tools, no edit or merge authority), and a
project that configures none sends nothing anywhere but Claude. Finally, an
`aswf score` command measures a provider's advisory review against the
planted violations in `tests/StyleCanary` before anyone turns it on. Wiring
the roles into the pipeline stages, triage (#86), writing tests or code, and
OmniRoute are not part of this item.

## Acceptance criteria

The `agents` section looks like this; every key is optional.

```json
{
  "agents": {
    "providers": {
      "gemini": { "kind": "gemini", "apiKeyVariable": "GEMINI_API_KEY" },
      "groq": { "kind": "groq", "apiKeyVariable": "GROQ_API_KEY" }
    },
    "roles": {
      "advisoryReview": ["gemini", "groq", "claude"],
      "summarise": ["groq", "claude"],
      "triage": []
    }
  }
}
```

`kind` is `gemini` or `groq`. `claude` is built in and is not declared under
`providers`. Role keys are `triage`, `summarise` and `advisoryReview`.

### Configuration

- **AC-1**: Given an `aswf.json` with no `agents` section, when
  `ConductorSettings.Load` reads it, then it succeeds and every role uses
  Claude only, exactly as before this item.
- **AC-2**: Given an `agents.roles` entry that names a provider which is
  neither `claude` nor declared under `agents.providers`, when
  `ConductorSettings.Load` reads it, then it fails with
  `SettingsUnreadable` whose message names the role and the unknown provider.
- **AC-3**: Given a provider whose `kind` is not `gemini` or `groq`, when
  `ConductorSettings.Load` reads it, then it fails with `SettingsUnreadable`
  whose message names the provider and the kind.
- **AC-4**: Given `agents.roles` names a key other than `triage`, `summarise`
  or `advisoryReview` (for example `specifier`, `implementer` or
  `codeReviewer`) with any provider other than `claude`, when
  `ConductorSettings.Load` reads it, then it fails with `SettingsUnreadable`
  whose message names the role and says only advisory roles may use other
  providers.
- **AC-5**: Given a role mapped to an empty list (`"triage": []`), when the
  role is requested, then no provider runs it and no prompt is sent to any
  provider; the caller receives a failed `Fin` carrying `RoleDisabled`
  (an `ExpectedFailure` naming the role) so it can skip the work.
- **AC-6**: Given a chain that includes a provider whose `apiKeyVariable`
  names an environment variable that is unset or empty, when the conductor
  starts (`aswf run` or `aswf score`), then it stops before any work with a
  message naming the provider and the variable, and never prints the key.

### Authority

- **AC-7**: Given an `AgentTask` whose `AgentRole` has any `Tools` or whose
  `Access` allows editing or commands, when a Gemini or Groq adapter is
  asked to `Run` it, then it throws `ArgumentException` (misconfiguration,
  per `IAgentic`) and makes no network call.
- **AC-8**: Given the new `WorkflowRoles.Summariser` and
  `WorkflowRoles.AdvisoryReviewer`, then each has `CapabilityTier.Small`,
  no tools, `AgentAccess.ToolsOnly` and `CanEdit` false, alongside the
  existing `WorkflowRoles.Triage`. The advisory reviewer's material (the
  diff) travels inside `AgentTask.Prompt`.
- **AC-9**: Given an `AgentTask` for any of the roles `Specifier`,
  `TestAuthor`, `TestReviewer`, `Implementer`, `CodeReviewer` or
  `Arbitrator`, when it is run through the routing agent, then it goes to
  Claude only, whatever `agents.roles` says.

### Providers

- **AC-10**: Given a Gemini or Groq adapter and a task, when `Run` is
  called, then it sends the prompt (plus `Instructions` as the system
  message when present) to that provider's HTTPS API with the key read from
  the configured environment variable, and returns a successful
  `AgentResult` holding the provider's text. The key appears in no log, no
  failure message and no `AgentResult`.
- **AC-11**: Given `AgentTask.OutputSchema` is set, when an adapter runs it,
  then it asks the provider for JSON; if the reply is not valid JSON, `Run`
  returns a failed `Fin` carrying `AgentOutputMalformed`.
- **AC-12**: Given the provider answers HTTP 429, when `Run` is called, then
  it returns a failed `Fin` carrying `UsageLimitReached`, with `ResetsAt`
  taken from the `Retry-After` header when present and `None` otherwise.
- **AC-13**: Given a timeout, a network error or any other non-success
  status, when `Run` is called, then it returns a failed `Fin` carrying
  `AgentTimedOut` for the timeout (`AgentTask.Timeout`) and
  `AgentProcessFailed` otherwise; it throws nothing.
- **AC-14**: Given the model for each `CapabilityTier`, then each adapter
  maps tiers to its own models in one place, as `ClaudeCapabilities` does,
  so no role names a model.

### Fallback chain

- **AC-15**: Given a role whose chain is `["gemini", "groq", "claude"]` and a
  first provider that succeeds, when the routing agent runs a task for that
  role, then only that provider is called and its result is returned.
- **AC-16**: Given the first provider fails with `UsageLimitReached` or any
  other expected failure, when the routing agent runs the task, then the
  next provider in the chain runs the same task, and so on in order.
- **AC-17**: Given a provider has returned `UsageLimitReached` with a
  `ResetsAt`, when the routing agent runs a later task before that time
  (by `TimeProvider`), then that provider is skipped without a call; when no
  `ResetsAt` was given it is skipped for 60 seconds. After that time it is
  tried again.
- **AC-18**: Given every provider in a chain that ends in `claude` has
  failed, when the routing agent returns, then it returns Claude's failure
  unchanged, so a `UsageLimitReached` from Claude is still waited out by
  `RunLoop`.
- **AC-19**: Given a chain that does not end in `claude` and every provider
  fails, when the routing agent returns, then it returns a failed `Fin`
  carrying `ProvidersExhausted`, whose message lists each provider with its
  failure, and it never calls Claude. `RunLoop` escalates it to a maintainer
  like any other failure.
- **AC-20**: Given cancellation is requested, when a provider is running,
  then the routing agent stops and does not try the next provider.
- **AC-21**: Given a provider falls back, then the routing agent writes one
  line to the log naming the role, the provider skipped and the reason.
  The prompt is never logged.

### Privacy

- **AC-22**: Given a role absent from `agents.roles`, when it runs, then it
  uses Claude only, and no provider named elsewhere in the file receives
  its prompt.
- **AC-23**: Given a provider appears in the chain of role A and not of
  role B, when role B runs, then that provider receives nothing.

### Scoring

- **AC-24**: Given `aswf score advisoryReview <provider>` (`<provider>` is
  `gemini`, `groq` or `claude`), when it runs, then it sends each file of
  `tests/StyleCanary` to that provider through `WorkflowRoles
  .AdvisoryReviewer` and asks for findings as JSON lines (file, line,
  message).
- **AC-25**: Given the findings, when scoring completes, then it prints
  recall (the share of `// expect:` markers whose line has a finding),
  false alarms (findings on lines with no marker), and the counts behind
  both, then exits 0; it never edits a file.
- **AC-26**: Given an unknown role or provider argument, or a provider not
  configured in `agents.providers`, when `aswf score` runs, then it exits
  non-zero with a message naming the problem and calls no provider.
- **AC-27**: Given a provider is rate-limited during scoring, when a file
  fails with `UsageLimitReached`, then scoring waits until `ResetsAt` (or
  60 seconds) and retries that file, rather than counting it as a miss.

## Out of scope

- Wiring any role into `SpecifyStage`, `BuildStage` or the run loop, and
  posting advisory comments on pull requests. Roles stay unwired until a
  provider has scored well.
- Triage itself (#86); this item only makes `triage` configurable.
- Summarisation scoring against `spec/`, writing tests or code, and mutation
  testing (#4).
- A threshold that decides whether a provider "scored well": a maintainer
  reads the numbers and edits `aswf.json`.
- OmniRoute or any proxy, and providers beyond Gemini and Groq.
- Giving any non-Claude provider tools, file access, edit or merge
  authority.

## Decisions

- **Role names are fixed to three.** `triage`, `summarise` and
  `advisoryReview` are the lowest-risk, measurable roles the maintainer named.
  Other roles cannot be routed away from Claude (AC-4, AC-9).
- **Two new roles, `Summariser` and `AdvisoryReviewer`.** Neither exists
  today in `WorkflowRoles`; both mirror `Triage` (no tools). The existing
  `CodeReviewer` and `TestReviewer` return verdicts that gate the build, so
  they cannot be advisory.
- **Providers get no tools.** They are plain HTTP chat APIs, so material is
  placed in the prompt. This is also what makes them safe under
  `WorkflowRoles` (AC-7).
- **Empty chain means off.** It is the explicit "none" for privacy (AC-5);
  an absent role means Claude, preserving today's behaviour (AC-22).
- **Rate limits are skipped, not waited, while a later provider exists.**
  Waiting happens only when Claude (or the scoring command) has to, reusing
  `UsageLimitReached` and `RunLoop`. The 60-second default when no reset time
  is given is my choice.
- **Any expected failure falls back,** not only rate limits, since free tiers
  are treated as unreliable.
- **Key from an environment variable,** never `aswf.json`, so secrets are not
  committed. The variable names are configurable per provider.
- **Scoring matches by file and line only,** using the existing
  `// expect:` markers; message wording is not judged. No pass mark is set.
- **Wiring is deferred,** since the maintainer's direction is to score before
  trusting.
