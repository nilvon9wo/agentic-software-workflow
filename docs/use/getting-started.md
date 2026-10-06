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

## Today: composing a worker's command line

The conductor's first component describes one headless Claude Code run. A role
is a model plus exactly the tools and permissions its job needs; a reviewer,
for example, can read but never edit:

<!-- snippet: compose-reviewer-invocation -->
```cs
ClaudeInvocation reviewer = ClaudeInvocation.Headless("sonnet")
    .WithTools(["Read", "Grep", "Glob"])
    .WithPermissionMode(ClaudePermissionMode.DontAsk)
    .WithSettingsFile(".claude/roles/code-reviewer.json");
```
<!-- endSnippet -->

`Arguments` is the exact argument list passed to the `claude` executable.

## Next: the workflow

Filing a GitHub issue and having the workflow specify, test, implement,
review, and open a pull request for it is the next milestone. The
[architecture](../how-it-works/architecture.md#the-pipeline) describes the
design; the
[issues](https://github.com/nilvon9wo/agentic-software-workflow/issues) track
progress.
