# Getting started

What you can use today, and what is on its way.

## Today: the quality gates

The gates work on their own, and they are the part of this project most worth
copying into other repositories:

```bash
scripts/gates.sh            # run every gate against the repository
scripts/gates.sh verify     # prove each gate still catches its canary violations
```

See [quality gates](../how-it-works/quality-gates.md) for what each gate checks
and [local development](../contribute/local-development.md) for setup —
including why, on Windows, the gates run in WSL.

## Today: running an AI worker

The conductor asks for AI work through one port, `IAgentic`. A role says
how capable a model it needs and which capabilities it is granted — never a
vendor's model or tool names — and a task gives it a prompt, a working
directory, and a time limit:

<!-- snippet: run-an-agent -->
```cs
AgentRole reviewer = new(
    CapabilityTier.Standard,
    [AgentTool.ReadFiles, AgentTool.SearchFiles],
    AgentAccess.ToolsOnly
);
AgentTask task = new(reviewer, "Review the change.", "/repository", TimeSpan.FromMinutes(5));
IAgentic runner = new ClaudeCodeAgentRunner(processRunner);

// Act
Fin<AgentResult> result = await runner.Run(task, cancellationToken);

// Assert
string outcome = result.Match(
    Succ: answer => answer.Text,
    Fail: error => $"failed: {error.Message}"
);
```
<!-- endSnippet -->

Expected failures — a timeout, a crash, an unreadable answer, an error the
agent reports — come back as a failed `Fin`, each its own type
(`AgentTimedOut`, `AgentProcessFailed`, …) carrying its own data, so the
workflow responds to each kind polymorphically rather than by decoding a
number. The
`ClaudeCodeAgentRunner` carries the task out as a headless `claude -p` run;
in production it is given a `SystemProcessRunner`.

## Today: specifying an issue

The first pipeline stage runs end to end. From the repository root, in WSL
(see [local development](../contribute/local-development.md#running-the-workflow)):

```bash
dotnet run --project src/AgenticSoftwareWorkflow.Cli -- specify 8
```

For issue 8, the conductor:

1. creates a fresh git worktree from the latest `master`;
2. briefs the specifier role with the issue, its own earlier questions, and
   its maintainers' replies — no one else's (to endorse someone else's
   comment, quote the part you agree with in your own reply);
3. either writes `spec/8.md` and opens a pull request for it, or posts its
   questions on the issue, labels it `needs-human`, and assigns it to you.

A specification defines what the tests and code will be held to, so its pull
request waits for your approval (`CODEOWNERS` makes `spec/` yours). Answer
questions in the issue thread, then run the command again. It sees that a
maintainer has replied since its question, removes `needs-human` and your
assignment, and the specifier reads your answers. Run it before you have
replied and it says the item is still waiting, without running the specifier.

## Today: letting it run

Instead of naming issues one at a time, let the conductor work through every
issue labelled `ready`:

```bash
dotnet run --project src/AgenticSoftwareWorkflow.Cli -- run          # keep watching
dotnet run --project src/AgenticSoftwareWorkflow.Cli -- run --once   # one pass, then stop
```

Each pass specifies every ready issue, then waits ten minutes and looks
again. An issue whose specification has been proposed moves from `ready` to
`specified`, so it is never specified twice. Issues waiting on you are
skipped until you reply.

- **Usage limit reached**: the conductor reads when the limit resets
  (Claude Code reports it), waits until a minute after, and retries the same
  issue. Nothing is lost and nothing needs you.
- **Anything else goes wrong**: it posts the failure on the issue and asks
  you (`needs-human`), rather than retrying every pass and burning usage.
  Reply once it is fixed, and the issue is picked up again.

Stop it with Ctrl+C at any time: its state lives in GitHub, not in the
process. One caveat: an issue stopped mid-way can leave its worktree under
`.aswf/worktrees/`; remove it (`git worktree remove --force <path>`) before
that issue runs again.

## Next

The remaining stages — tests, implementation, review, repair — follow the
[architecture](../how-it-works/architecture.md#the-pipeline); the
[issues](https://github.com/nilvon9wo/agentic-software-workflow/issues) track
progress.
