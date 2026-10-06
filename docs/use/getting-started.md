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

## Next

The remaining stages — tests, implementation, review, repair — follow the
[architecture](../how-it-works/architecture.md#the-pipeline); the
[issues](https://github.com/nilvon9wo/agentic-software-workflow/issues) track
progress.
