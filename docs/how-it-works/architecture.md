# Architecture

How this project turns the [vision](../vision.md) into a working system — and
why each piece is built (or *not* built) the way it is.

> **Status.** The foundation is in place: the quality gates, their canary, CI,
> and the first conductor component. The pipeline stages below are the design
> being built next, tracked as GitHub issues. Each section says which parts
> exist today.

## The one idea that matters most

Reliability does not come from smarter prompts. It comes from two things:

1. **Deterministic checks wherever a check can be deterministic.** A compiler,
   a linter, a test runner, and a coverage threshold cannot be talked into
   passing. Models are used only for judgments software cannot make.
2. **Separation of authority.** No agent grades its own work, and no agent can
   change the definition of success. The implementer cannot see withheld tests
   or edit the specification; reviewers can read but not write.

Everything below is machinery for those two ideas.

## Vocabulary

"Agent" gets used for everything. This project keeps the terms apart:

| Term | What it is here | Deterministic? |
| --- | --- | --- |
| **Workflow** | The outer loop: stage order, retries, escalation, waiting on humans. Owned by the conductor. | Yes |
| **Conductor** | The small C# program that runs the workflow. It decides *what* runs next; models never do. | Yes |
| **Role** | A job description for one model run: model, permitted tools, permission mode, settings, and skill. | Configuration |
| **Worker** | One AI run playing one role, on one task, then exiting — today a headless Claude Code run (`claude -p`). | No |
| **Port** | An interface the conductor depends on instead of a vendor: `IAgentRunner` (who does the AI work), `IWorkSource` (where work and answers come from). Claude Code and GitHub are adapters behind them. | Yes |
| **Skill** | Reusable *how-to* instructions (`.claude/skills/*/SKILL.md`), loaded on demand. Advice, not enforcement. | — |
| **Hook** | A script Claude Code runs at lifecycle events (e.g. before a tool call). It can *block* actions. Enforcement, not advice. | Yes |
| **Tool** | A capability a worker can invoke: read, edit, run a command. Built into Claude Code. | Yes |
| **MCP** | A protocol for exposing extra tools to a model. Not used yet — built-in tools plus the `gh` CLI cover every need so far, and each MCP server is more attack surface and more tokens in context. | — |
| **Gate** | A deterministic quality check with a pass/fail verdict. See [quality gates](quality-gates.md). | Yes |
| **Evaluator** | A *read-only* worker judging what no gate can (naming, test usefulness). It reports; it never fixes. | No |

The distinction that does the most work: **a skill tells a model what it
should do; a hook or a permission rule decides what it can do.** Anything that
must be true is never left to a skill.

## Prior art first

Building every piece from scratch would be educational and slow, and it would
ignore tools that already do the job well. This project assembles free,
established components and writes custom code only where nothing suitable
exists:

| Need | Off-the-shelf choice | Why |
| --- | --- | --- |
| Agent runtime, tools, sub-agents, skills, hooks | **Claude Code** (headless `claude -p`), behind `IAgentRunner` | Already does tool calling, context management, and permissions well; included in a Pro subscription |
| Work queue, human questions, state, audit trail | **GitHub Issues, labels, PRs**, behind `IWorkSource` | Free, durable, visible, and it comes with a UI for the human |
| CI | **GitHub Actions** | Free for public repositories |
| C# style and correctness | **Roslyn analyzers + `.editorconfig`**, **`dotnet format`**, **ReSharper CLI `inspectcode`** (free) | Each catches things the others miss — see [quality gates](quality-gates.md) |
| Coverage | **coverlet** (`coverlet.MTP`) | Enforces a threshold inside `dotnet test` |
| Mutation testing | **Stryker.NET** | Measures whether tests *detect* bugs, not just execute lines |
| Executable documentation | **MarkdownSnippets** (`mdsnippets`) | Doc code blocks are copied from compiled, tested code |
| Python / shell / Markdown / workflow lint | **ruff**, **pyright**, **shellcheck**, **markdownlint**, **lychee**, **actionlint** | Every language held to equivalent standards |
| Test data | **Xfty** | The maintainer's own test data factory, used where tests need records |

Custom code exists only for what has no off-the-shelf answer: the conductor's
workflow logic, the gate runner that turns every tool into one verdict, the
canary that proves the gates work, and a layout check no analyzer performs.

## The pipeline

Each stage is a role. The conductor runs them in order, gives each one only
the artifacts it is entitled to, and checks the result with gates before
moving on.

```text
GitHub issue (feature / bug)
   │
   ├─ 1. Clarify        ── cheap model summarises sources; questions → `needs-human` issue
   ├─ 2. Specify        ── specification + acceptance criteria, committed
   ├─ 3. Write tests    ── visible tests (implementer sees) + withheld tests (it never does)
   ├─ 4. Review tests   ── read-only evaluator + lint gates
   ├─ 5. Implement      ── isolated git worktree: spec + visible tests only
   ├─ 6. Gate           ── run_gates.py, then the withheld tests, then mutation testing
   ├─ 7. Review code    ── read-only evaluator
   ├─ 8. Repair loop    ── failures go back to (5), bounded attempts
   ├─ 9. Arbitrate      ── repeated failure: is the spec, the tests, or the code wrong?
   └─10. Pull request   ── auto-merge once every gate is green
```

### No vendor lock-in

Claude and GitHub are today's choices, not assumptions baked into the
conductor. The conductor talks to two ports:

- **`IAgentRunner`** — "run this role on this task and give me its result".
  The Claude Code adapter composes a `ClaudeInvocation`; another adapter could
  drive a different agent CLI or a model API, from Anthropic or anyone else.
  Roles name a *capability tier* (small, standard, strongest), and each
  adapter maps tiers to its own models.
- **`IWorkSource`** — "what work is ready, and what have humans said". GitHub
  Issues is the first adapter; others (a different tracker, a folder of
  Markdown files, several sources at once) can be added without touching the
  workflow.

Separation of authority must survive the swap: an adapter that cannot enforce
a role's tool and path restrictions cannot run that role.

### Where the human comes in

Humans answer questions, not supervise steps.

**Asking.** A worker that hits genuine ambiguity does not guess. It posts its
question as a comment on the issue — specific, with the options it sees and
what each would mean — and adds the `needs-human` label. The conductor moves
on to work that does not depend on the answer.

**Waiting is visible.** An issue is *waiting on a human* exactly when it
carries `needs-human` — not merely because humans have commented on it. The
worker also assigns the issue to the person whose answer it needs, so it
appears in their own list (`is:open assignee:@me label:needs-human` on
GitHub). Answering removes both the label and the assignment, so the two
views never disagree.

**Answering.** The human replies in the issue thread, as in any conversation.
The conductor notices the new comment, and a worker reads the whole thread.
It either asks a follow-up (a genuine two-way dialogue) or records the
decision — in the specification, with a link back to the thread — and
removes the label. The thread is the audit trail of why the software behaves
as it does.

**Trust.** The repository is public, so anyone can comment. Only comments from
the maintainers count as answers; everything else is untrusted input, and
treated as data, never as instructions. This matters: text an AI reads can try
to steer it (prompt injection), and issue comments are text anyone can write.

**Triage.** A human is not needed to triage everything. A triage worker can
label, de-duplicate, link related issues, and judge whether an issue meets the
definition of ready (clear goal, acceptance criteria, no open questions) —
promoting it to `ready` or asking what is missing. A human decides priority
and scope when issues compete, and can override any triage decision.

**Feedback.** Manual and exploratory testing feed back the same way, as
issues.

### Separation of authority, mechanically

| Rule | Mechanism |
| --- | --- |
| The implementer never sees withheld tests | They live outside the implementer's git worktree, and a permission deny rule blocks the path anyway |
| Evaluators cannot change what they judge | Their role grants only read tools (`Read`, `Grep`, `Glob`) |
| Nobody silently changes the spec | Spec files are write-denied to every role except the specifier; changes come back as `needs-human` issues |
| An unattended run cannot skip permission checks | The conductor's `ClaudePermissionMode` has no `bypassPermissions` value — it cannot even be expressed |

Here is a code-reviewer role as the conductor composes it — read tools only,
anything not explicitly allowed is denied, plus a role-specific settings file:

<!-- snippet: compose-reviewer-invocation -->
```cs
ClaudeInvocation reviewer = ClaudeInvocation.Headless("sonnet")
    .WithTools(["Read", "Grep", "Glob"])
    .WithPermissionMode(ClaudePermissionMode.DontAsk)
    .WithSettingsFile(".claude/roles/code-reviewer.json");
```
<!-- endSnippet -->

## Cost

The design target is **no spend beyond a Claude Pro subscription**:

- Workers run through Claude Code on the subscription — no pay-per-token API.
  (Another provider can be plugged in behind `IAgentRunner`.)
- Work is **serial by default**. Parallel workers multiply usage, and Pro
  limits are tight.
- Each role uses the cheapest model that does the job: a small model for
  summarising and triage, larger ones only for specification and implementation.
- Workers get artifacts (a spec file, a test file), not conversation history,
  so context stays small.
- Every deterministic check costs zero tokens. The more gates catch, the fewer
  model calls are spent on review and repair.

A local model through Ollama remains an option for embeddings and trivial
classification, but on modest hardware it is not good for much more.

## What to take away

- **Gates beat prompts.** Each check a tool can do is one a model no longer
  needs to do — and a tool cannot be argued with.
- **Check the checkers.** A gate that silently stops running looks exactly like
  one that passes. The [canary](quality-gates.md#proving-the-gates-work) exists
  because of real gaps found while building this.
- **Enforce with permissions and hooks; advise with skills.**
- **Artifacts, not conversations.** Workers communicate through files and
  issues, which keeps context small and leaves an audit trail.
