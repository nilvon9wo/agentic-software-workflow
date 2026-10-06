# Agentic Software Workflow

A deliberately small, from-scratch C#/.NET experiment in deterministic workflow orchestration combined with bounded AI steps.

## What this first slice does

```text
requirement
    ↓
Claude: requirements → specification
    ↓
Claude: specification → visible + hidden tests
    ↓
Claude: specification + visible tests → implementation
    ↓
deterministic: build
    ↓
deterministic: visible tests
    ↓
deterministic: hidden tests
    ↓
Claude: read-only code evaluation
```

The implementation AI is never given the hidden-test contents by the workflow. The code evaluator is read-only by design: it emits an evaluation artifact rather than modifying source. The current v0 enforces the AI boundary by controlling what artifacts are placed into the implementation prompt; OS-level capability isolation comes later.

This is intentionally not yet a general-purpose agent framework. The first goal is to make the boundaries observable and then evolve them based on actual use.

## Requirements

- .NET 10 SDK
- An Anthropic API key

> A Claude consumer subscription and Claude API access are separate billing/access mechanisms. This project currently calls the Anthropic API directly, so it requires API credentials and API billing/credits.

## Configure

PowerShell:

```powershell
$env:ANTHROPIC_API_KEY = "your-api-key"
$env:ANTHROPIC_MODEL = "claude-haiku-4-5-20251001"
```

The default model is Haiku 4.5 to keep the first experiments inexpensive. Override `ANTHROPIC_MODEL` when a stronger model is useful.

## Run the framework tests

```powershell
dotnet test
```

## Run the first workflow

```powershell
dotnet run --project src/AgenticWorkflow
```

Generated artifacts and the test subject appear under `workspace/`.

## Important current limitations

This is intentionally a v0:

- no MCP yet
- no skills yet
- no persistent workflow-instance state
- no resumability
- no retries or repair loops
- no human gates
- no parallel scheduling
- no formal capability/permission enforcement yet
- no GitHub integration
- AI output is JSON-constrained by prompting and lightly parsed rather than using provider-native structured output

Those are upcoming experiments, not accidental omissions.
