# Local development

## Prerequisites

| Tool | Why |
| --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Builds and tests; `global.json` pins the feature band |
| Python 3.12+ with `venv` | Runs the gate scripts; their tools install into a local virtual environment |
| [Claude Code](https://claude.com/claude-code) | Runs the AI workers (a Pro subscription is enough) |
| [GitHub CLI](https://cli.github.com/) (`gh`) | Issues and pull requests — the workflow's queue |

Everything else (ruff, pyright, shellcheck, ReSharper CLI, mdsnippets,
Stryker.NET) is pinned in `requirements-dev.txt` and `dotnet-tools.json`, and
installed by `scripts/gates.sh` on first run.

## On Windows: run the gates in WSL

Windows **Smart App Control** intermittently blocks test DLLs, because coverage
instrumentation rewrites them on every run and the rewritten files are
unsigned. The symptom is a test failure with `An Application Control policy has
blocked this file (0x800711C7)`. Turning Smart App Control off is
irreversible without reinstalling Windows, so instead the gates run in WSL —
which is also the closest local match to the Linux CI runner.

One-time setup, in an Ubuntu (WSL) terminal:

```bash
sudo apt install -y python3-venv   # Ubuntu ships Python without venv support
# .NET 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux-ubuntu
```

Then, from the repository directory in WSL:

```bash
scripts/gates.sh
```

Windows and Linux builds of the same checkout write to separate output roots
(`.artifacts/windows`, `.artifacts/linux`; see `Directory.Build.props`), so
building from both sides does not cause conflicts.

## The loop

```bash
scripts/gates.sh                 # everything; the same check CI runs
scripts/gates.sh run build test  # a subset while iterating
dotnet format AgenticSoftwareWorkflow.slnx   # auto-fix formatting
python -m ruff check --fix scripts tests/scripts   # auto-fix what ruff safely can (imports, trailing commas)
dotnet mdsnippets                # refresh documentation snippets after changing a snippet's source
```

`run_gates.py` never stops at the first failure: one run shows everything that
needs fixing.

## Writing a documentation snippet

Code shown in the docs is never typed into the Markdown. It lives in a test,
between markers:

```csharp
// begin-snippet: my-example
...code...
// end-snippet
```

and the Markdown names it on a line of its own — the word `snippet`, a colon,
and the snippet's name (an example cannot be shown here, because `mdsnippets`
would expand it). `dotnet mdsnippets` copies the code in. CI regenerates the snippets and fails
if anything changes, so documentation cannot drift from the tested code.

## Live checks

Separation of authority is proven against a real Claude Code run, not just
unit-tested. These checks spend subscription usage, so they never run in CI or
in `scripts/gates.sh`. Run them on purpose — after changing a role's access
rules, and after upgrading Claude Code:

```bash
scripts/live-checks.sh
```

They need `claude` installed and signed in **inside WSL** (or Linux): on
Windows, headless Claude Code offers PowerShell rather than Bash, so the
command allow-lists would not be tested. To install there:

```bash
curl -fsSL https://claude.ai/install.sh | bash
claude   # sign in once
```

Each check asserts both that nothing leaked or changed *and* that a refusal was
recorded — a run in which the model never tried would otherwise pass while
proving nothing.

## Running the workflow

`aswf` runs from the repository root, where `aswf.json` says which repository
the work lives in, who the maintainers are, and who commits on the workers'
behalf. It needs, **in WSL**:

- `claude`, signed in (the workers);
- `gh`, signed in as the **workers' account** — never the maintainer's, so the
  maintainer's comments are distinguishable as answers and the maintainer can
  approve the workers' pull requests;
- `git` pushing through that same account (`gh auth login` sets this up).

```bash
dotnet run --project src/AgenticSoftwareWorkflow.Cli -- specify <issue-number>
```

Each run works in its own git worktree under `.aswf/worktrees/` (ignored by
git) and removes it afterwards.
