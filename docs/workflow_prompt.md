I want to build a reusable, generic **agentic software-development workflow/orchestration system** as a learning project and as a tool I can reuse for future software projects.

The eventual goal is roughly:

```
Human provides one or more source documents describing a desired application/feature
    ↓
Requirements ingestion and analysis
    ↓
Human clarification when requirements are ambiguous
    ↓
Formalized specification / acceptance criteria
    ↓
Test architecture and test generation
    ↓
Test-quality review
    ↓
Implementation by a separate AI
    ↓
Deterministic and AI-based quality gates
    ↓
Automated repair loops
    ↓
Conflict analysis when repeated failures suggest disagreement between
specification, tests, and implementation
    ↓
Documentation generation and executable-documentation verification
    ↓
Human manual/exploratory testing
    ↓
GitHub issues / further development
    ↓
Repeat until the project is accepted
```

The important architectural principle is that I do NOT want one giant autonomous AI agent controlling everything.

I want a deterministic **workflow orchestrator** to control the overall process, with bounded AI loops inside appropriate workflow stages.

The system should distinguish clearly between:

1. WORKFLOWS

   * Control sequencing, state transitions, retries, gates, permissions,
     human waits, escalation, and parallel work.
   * Deterministic code should enforce deterministic rules whenever possible.
   * The AI should not be asked to make decisions that ordinary software
     can make reliably.

2. AI LOOPS

   * Used only where reasoning/judgment is useful.
   * The application/workflow should generally own the outer loop.
   * Individual AI workers may have bounded internal agentic loops.

3. SKILLS

   * Reusable procedural knowledge/instructions explaining HOW an AI should
     perform a particular class of task.
   * Skills should generally be separate from orchestration and actual tools.
   * Markdown-based skills are acceptable where appropriate.

4. TOOLS / MCP

   * Actual capabilities the AI can invoke.
   * Examples include reading/writing files, searching code, running tests,
     invoking builds, querying systems, creating GitHub issues, etc.
   * MCP should be used where it provides useful standardized capability
     exposure.
   * Do not use MCP merely because it is fashionable; understand what it
     contributes.

5. PLUGINS / EXTERNAL INTEGRATIONS

   * Access to external systems such as GitHub, CI/CD, document stores,
     issue trackers, etc.
   * Keep external integrations behind appropriate capability boundaries.

6. DETERMINISTIC QUALITY GATES

   * Linters
   * compiler/build
   * unit/integration/E2E tests
   * static analysis
   * mutation testing
   * performance/load testing
   * documentation example execution
   * security scans
   * etc.
     These should not be delegated to an LLM when deterministic tooling can
     perform the check.

7. AI EVALUATORS / JUDGES

   * Used for qualities that deterministic tools cannot adequately evaluate.
   * Test quality
   * requirement coverage
   * architectural quality
   * naming and intent
   * useful assertions
   * appropriate test pyramid
   * comments explaining WHY rather than WHAT
   * SOLID/DRY/YAGNI considerations
   * complexity and maintainability
   * etc.
   * Evaluators should normally be read-only with respect to the artifact
     they are judging.

8. HUMAN GATES

   * Genuine ambiguity
   * specification changes
   * unresolved conflicts
   * exploratory testing
   * decisions requiring human judgment

The framework should maintain persistent artifacts rather than passing huge
amounts of conversational context from one AI to another.

For example, conceptually:

```
/specification
    requirements.md
    acceptance-criteria.md
    open-questions.md

/tests
    unit/
    integration/
    e2e/
    ui/
    performance/
    load/
    mutation/
    hidden/

/quality
    test-review/
    code-review/
    lint-results/
    test-results/

/documentation
    getting-started.md
    architecture.md
    contributing.md
    manual-test-plan.md
```

The exact structure should be designed together rather than assumed.

A particularly important requirement is separation of authority.

For example:

* The implementation AI must not be allowed to modify hidden tests.
* A test evaluator must not modify the tests it is evaluating.
* A code evaluator must not modify the implementation it is evaluating.
* An implementation worker should not silently modify the specification.
* Proposed specification changes should be treated differently from ordinary
  implementation changes.
* The system must avoid the failure mode where the agents gradually change
  the definition of success until the implementation passes.

The eventual system should support hidden tests. The implementation AI should
receive the specification and intentionally visible tests, while hidden tests
remain in a separate evaluation boundary.

When repeated failures occur, the workflow should eventually invoke a
separate conflict-analysis AI that receives the relevant specification,
tests, implementation, evaluator findings, and failure history and determines
whether:

* the implementation is wrong;
* tests are wrong;
* the specification is wrong or ambiguous;
* multiple artifacts disagree;
* or human clarification is required.

The system should also be capable of continuing independent work while one
workflow branch is waiting for human input.

For example:

```
Feature A → waiting for human clarification
Feature B → test generation
Feature C → implementation
Feature D → documentation
```

The orchestration layer should be capable of scheduling those independently
where dependencies permit.

The eventual documentation phase should generate documentation and then
verify it mechanically wherever possible. For example, documented commands
and code examples should actually be executed rather than merely reviewed by
an AI.

Human exploratory testing should feed defects back into the system, ideally
through GitHub issues or an equivalent issue mechanism.

TECHNOLOGY / LEARNING CONSTRAINTS

I am primarily a C#/.NET developer, so C# should be the primary implementation
language unless there is a compelling technical reason to use another
language.

I want this to be a hands-on learning project. I want to understand the
machinery, not merely download a framework and configure it.

I am particularly interested in understanding:

* AI loops
* tool calling
* MCP servers and clients
* skills
* workflow orchestration
* AI evaluators
* deterministic versus AI-controlled decisions
* context/token efficiency
* human-in-the-loop patterns
* parallel agent work
* artifact-based communication between agents
* permissions/capability boundaries
* failure/retry/escalation strategies
* how different AI models can be assigned different jobs

I have access to a relatively inexpensive Claude subscription and would like
to keep AI/API costs low. I do not want the project to depend on GPU-heavy
local models.

Do not assume that everything needs to be implemented immediately.

STARTING APPROACH

I want us to build this incrementally.

First, help me design the smallest useful architecture that demonstrates the
fundamental concepts.

Then we should implement a minimal vertical slice, probably something like:

```
requirement
   ↓
specification
   ↓
AI-generated tests
   ↓
AI implementation
   ↓
deterministic test execution
   ↓
AI evaluation
   ↓
pass/fail/escalation
```

The first example project should be deliberately trivial. Its purpose is to
exercise the machinery rather than demonstrate impressive AI coding ability.

Once the vertical slice works, we can progressively add:

* skills
* MCP
* additional tools
* multiple AI roles/models
* hidden tests
* AI test evaluation
* AI code evaluation
* repair loops
* conflict analysis
* human gates
* parallel work
* documentation verification
* GitHub integration
* persistent workflow state
* resumability
* cost/context optimization

Please do NOT begin by generating a giant codebase.

Instead:

1. Establish the architectural model.
2. Identify the minimum components.
3. Explain why each component exists.
4. Decide which responsibilities belong to deterministic code versus AI.
5. Define the first vertical slice.
6. Choose the simplest practical technology for each component.
7. Build it incrementally, with working software at every stage.

I strongly prefer explicit C# types, explicit `this`, composition over
inheritance, clear separation of concerns, and relatively low-copy,
straightforward code.

When there is a choice between a sophisticated framework and a small amount
of understandable code, initially prefer the understandable code. We can
introduce frameworks later when doing so teaches or provides something
meaningful.

Do not assume that an existing agent framework is automatically the correct
architecture. We are building this partly to understand what those
frameworks are actually doing.

Also, distinguish carefully between:

* MCP as a protocol,
* an MCP server,
* an MCP client,
* a tool,
* an AI tool,
* an AI loop,
* a workflow,
* a skill,
* a plugin/integration,
* and an AI evaluator.

I want to understand the boundaries between these things rather than having
them blurred together under the generic term "agent."

Before writing substantial code, propose the architecture and the first
vertical slice, and explain the reasoning behind the design.
