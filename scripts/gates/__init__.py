"""The quality gates, defined once.

`run_gates.py` runs them against the real code (locally, in CI, and as the
deterministic check every AI worker must pass). `verify_gates.py` runs them
against tests/StyleCanary and proves each one still reports what it should.
One definition means "the gates passed" can never mean two different things.

- `model`: what the gates work with (targets, findings, results).
- `tools`: running external tools and reading their reports.
- `repository_files`: which files are the repository's own.
- `dotnet_gates`, `layout_gate`, `python_gates`, `shell_gates`,
  `markdown_gates`, `workflow_gate`, `snippet_gate`: the gates, grouped by
  what they check.
- `registry`: which gates exist, in the order they run.
"""
