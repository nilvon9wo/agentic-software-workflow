# Agentic Software Workflow

Turn a specification into working, tested, documented software with AI workers
— reliably, with a human involved only where human judgment is needed, and
within a Claude Pro subscription.

The approach: a small deterministic **conductor** runs the workflow, headless
**Claude Code** workers do the judgment work in narrowly permitted roles, and
**quality gates** that cannot be argued with decide whether the work is done.
GitHub issues are the queue, and the project is built by its own workflow.

> **Status: foundation.** The quality gates (with a canary proving each one
> works), CI, and the conductor's first component are in place. The pipeline
> stages are being built next — see the
> [issues](https://github.com/nilvon9wo/agentic-software-workflow/issues).

## Documentation

| Page | Covers |
| --- | --- |
| [Vision](docs/vision.md) | What this project is ultimately for |
| [Architecture](docs/how-it-works/architecture.md) | How it works, and why it is built this way |
| [Quality gates](docs/how-it-works/quality-gates.md) | Every check, what it catches, and how the checks are themselves checked |
| [Getting started](docs/use/getting-started.md) | Using it today |
| [Contributing](docs/contribute/README.md) | Standards and local development, for humans and AI alike |

## Quick start

```bash
git clone https://github.com/nilvon9wo/agentic-software-workflow.git
cd agentic-software-workflow
scripts/gates.sh          # run every quality gate (on Windows, from WSL)
```

## License

[MIT](LICENSE)
