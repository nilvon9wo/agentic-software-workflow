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

The conductor asks for AI work through one port, `IAgentRunner`. A role says
how capable a model it needs and which capabilities it is granted — never a
vendor's model or tool names — and a task gives it a prompt, a working
directory, and a time limit:

<!-- snippet: run-an-agent -->
```cs
AgentRole reviewer = new(CapabilityTier.Standard, [AgentTool.ReadFiles, AgentTool.SearchFiles]);
AgentTask task = new(reviewer, "Review the change.", "/repository", TimeSpan.FromMinutes(5));
IAgentRunner runner = new ClaudeCodeAgentRunner(processRunner);

// Act
result = await runner.RunAsync(task, cancellationToken);

// Assert
string outcome = result.Match(
    Succ: answer => answer.Text,
    Fail: error => $"failed ({error.Code}): {error.Message}"
);
```
<!-- endSnippet -->

Expected failures — a timeout, a crash, an unreadable answer, an error the
agent reports — come back as a failed `Fin`, each with a stable code from
`AgentErrors`, so the workflow decides what happens next. The
`ClaudeCodeAgentRunner` carries the task out as a headless `claude -p` run;
in production it is given a `SystemProcessRunner`.

## Next: the workflow

Filing a GitHub issue and having the workflow specify, test, implement,
review, and open a pull request for it is the next milestone. The
[architecture](../how-it-works/architecture.md#the-pipeline) describes the
design; the
[issues](https://github.com/nilvon9wo/agentic-software-workflow/issues) track
progress.
