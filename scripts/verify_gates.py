#!/usr/bin/env python3
"""Prove every quality gate still catches what it is supposed to catch.

A gate that silently stops running looks exactly like a gate that passes.
So tests/StyleCanary holds deliberately broken code, each violation tagged
`expect: <gate>:<rule>` on the line a gate must report. This script runs
the static gates against the canary and fails if any expected finding is
missing.

The two test gates are proven separately: a throwaway uncovered function
is added to the real code, and the suite must then fail its 100% coverage
threshold. The snippet gate is proven the same way, with a throwaway
document whose snippet no longer matches the tested code.

Usage: verify_gates.py
"""

import re
import sys
from collections.abc import Iterable, Sequence
from pathlib import Path

from exit_codes import exit_code_for
from gates.model import WHOLE_FILE, Finding, Gate, GateResult, Target
from gates.registry import (
    PYTHON_TEST_GATE,
    SNIPPETS_GATE,
    STATIC_GATES,
    TEST_GATE,
)
from gates.tools import REPOSITORY_ROOT

CANARY_DIRECTORY = Path("tests/StyleCanary")
CANARY = Target(
    dotnet_project=CANARY_DIRECTORY / "StyleCanary.csproj",
    csharp_paths=[CANARY_DIRECTORY / "Violations.cs"],
    python_paths=[CANARY_DIRECTORY / "violations.py"],
    shell_paths=[CANARY_DIRECTORY / "violations.sh"],
    markdown_paths=[CANARY_DIRECTORY / "violations.md"],
    workflow_paths=[CANARY_DIRECTORY / "violations.yml"],
)
REPOSITORY = Target(
    dotnet_project=Path("AgenticSoftwareWorkflow.slnx"),
    csharp_paths=[],
    python_paths=[],
    shell_paths=[],
    markdown_paths=[],
    workflow_paths=[],
)
# `//` in C#, `#` in Python, shell and YAML, `<!--` in Markdown.
EXPECT_MARKER = re.compile(
    r"(?://|#|<!--) expect: (?P<gate>[\w-]+):(?P<rule>[\w-]+)",
)
UNCOVERED_CSHARP_PATH = Path(
    "src/AgenticSoftwareWorkflow.Conductor/GateCanaryUncovered.cs",
)
UNCOVERED_CSHARP = (
    "namespace AgenticSoftwareWorkflow.Conductor;\n"
    "\n"
    "public static class GateCanaryUncovered\n"
    "{\n"
    "    public static int Answer() => 42;\n"
    "}"
)
STALE_SNIPPET_PATH = Path("docs/gate-canary-stale-snippet.md")
STALE_SNIPPET = (
    "# Stale snippet\n"
    "\n"
    "<!-- snippet: run-an-agent -->\n"
    "```cs\n"
    "// Not what the test says.\n"
    "```\n"
    "<!-- endSnippet -->\n"
)
UNCOVERED_PYTHON_PATH = Path("scripts/lint/gate_canary_uncovered.py")
UNCOVERED_PYTHON = (
    '"""Uncovered on purpose."""\n'
    "\n"
    "\n"
    "def answer() -> int:\n"
    '    """Never called."""\n'
    "    return 42\n"
)


def expectation(
    path: Path,
    line_number: int,
    match: re.Match[str],
) -> Finding:
    """The Finding one `expect:` marker demands."""
    return Finding(match["gate"], match["rule"], path.name, line_number)


def expectations_in_line(
    path: Path,
    line_number: int,
    line: str,
) -> list[Finding]:
    """The Findings demanded by the `expect:` markers on one line."""
    matches = EXPECT_MARKER.finditer(line)
    return [expectation(path, line_number, match) for match in matches]


def expectations_in(path: Path) -> list[Finding]:
    """Every `expect:` marker in one canary file."""
    absolute_path = REPOSITORY_ROOT / path
    text = absolute_path.read_text(encoding="utf-8")
    lines = text.splitlines()
    numbered_lines = enumerate(lines, start=1)
    return [
        expected
        for number, line in numbered_lines
        for expected in expectations_in_line(path, number, line)
    ]


def matches_expectation(found: Finding, expected: Finding) -> bool:
    """Same rule, same file; on the expected line or the whole file."""
    is_same_rule = found.rule == expected.rule
    is_same_file = found.file_name == expected.file_name
    accepted_lines = {expected.line_number, WHOLE_FILE}
    is_same_line = found.line_number in accepted_lines
    return (
        is_same_rule
        and is_same_file
        and is_same_line
    )


def is_reported(expected: Finding, findings: Iterable[Finding]) -> bool:
    """True when any finding satisfies the expectation."""
    matching_findings = [
        found
        for found in findings
        if matches_expectation(found, expected)
    ]
    return len(matching_findings) > 0


def missing_findings(
    expectations: Sequence[Finding],
    results: Sequence[GateResult],
) -> list[Finding]:
    """Expectations that no gate of the expected name reported."""
    findings_by_gate = {result.gate: result.findings for result in results}
    missing: list[Finding] = []
    for expected in expectations:
        gate_findings = findings_by_gate.get(expected.gate, [])
        if not is_reported(expected, gate_findings):
            missing.append(expected)
    return missing


def unproven_gates(expectations: Sequence[Finding]) -> list[str]:
    """Static gates with no canary marker exercising them."""
    exercised = {expected.gate for expected in expectations}
    all_gates = {gate.name for gate in STATIC_GATES}
    return sorted(all_gates - exercised)


def describe_missing(expected: Finding) -> str:
    """One line naming an expected finding no gate reported."""
    location = f"{expected.file_name}:{expected.line_number}"
    return (
        f"[MISSED] {expected.gate} did not report {expected.rule} at {location}"
    )


def verify_static_gates() -> bool:
    """Run each static gate on the canary; every marker must be matched."""
    canary_files = [
        *CANARY.csharp_paths,
        *CANARY.python_paths,
        *CANARY.shell_paths,
        *CANARY.markdown_paths,
        *CANARY.workflow_paths,
    ]
    expectations = [
        expected
        for path in canary_files
        for expected in expectations_in(path)
    ]
    results = [gate.run(CANARY) for gate in STATIC_GATES]
    missing = missing_findings(expectations, results)
    unproven = unproven_gates(expectations)
    for expected in missing:
        print(describe_missing(expected))
    for gate_name in unproven:
        print(f"[UNPROVEN] no canary marker exercises the '{gate_name}' gate")
    reported_count = len(expectations) - len(missing)
    expected_count = len(expectations)
    print(f"static gates: {reported_count}/{expected_count} findings reported")
    return not missing and not unproven


def fails_with_uncovered_code(gate: Gate, path: Path, source: str) -> bool:
    """True when the test gate fails once the code has an uncovered part."""
    uncovered_file = REPOSITORY_ROOT / path
    uncovered_file.write_text(source, encoding="utf-8")
    try:
        result = gate.run(REPOSITORY)
    finally:
        uncovered_file.unlink()
    is_caught = not result.has_passed
    print(
        f"{gate.name} coverage gate caught an uncovered addition: {is_caught}",
    )
    return is_caught


def fails_with_stale_snippet() -> bool:
    """True when the snippet gate fails on a document showing stale code.

    The document is temporary, and in the real repository: one checked in
    would be rewritten by every real run of `dotnet mdsnippets`.
    """
    stale_file = REPOSITORY_ROOT / STALE_SNIPPET_PATH
    stale_file.write_text(STALE_SNIPPET, encoding="utf-8")
    try:
        result = SNIPPETS_GATE.run(REPOSITORY)
    finally:
        stale_file.unlink()
    is_caught = not result.has_passed
    print(f"snippets gate caught a stale snippet: {is_caught}")
    return is_caught


def main() -> int:
    """Verify every gate; exit 1 if any gate failed to catch its canary."""
    is_static_proven = verify_static_gates()
    is_snippets_proven = fails_with_stale_snippet()
    is_csharp_coverage_proven = fails_with_uncovered_code(
        TEST_GATE,
        UNCOVERED_CSHARP_PATH,
        UNCOVERED_CSHARP,
    )
    is_python_coverage_proven = fails_with_uncovered_code(
        PYTHON_TEST_GATE,
        UNCOVERED_PYTHON_PATH,
        UNCOVERED_PYTHON,
    )
    is_every_gate_proven = (
        is_static_proven
        and is_snippets_proven
        and is_csharp_coverage_proven
        and is_python_coverage_proven
    )
    return exit_code_for(is_every_gate_proven)


if __name__ == "__main__":
    sys.exit(main())
