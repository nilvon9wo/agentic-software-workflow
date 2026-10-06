#!/usr/bin/env python3
"""Prove every quality gate still catches what it is supposed to catch.

A gate that silently stops running looks exactly like a gate that passes. So
tests/StyleCanary holds deliberately broken code, each violation tagged
`expect: <gate>:<rule>` on the line a gate must report. This script runs the
static gates against the canary and fails if any expected finding is missing.

The test gate is proven separately: a throwaway uncovered class is added to the
real library, and the suite must then fail its 100% coverage threshold.

Usage: verify_gates.py
"""

import re
import sys
from collections.abc import Iterable, Sequence
from pathlib import Path

from gates import REPOSITORY_ROOT, STATIC_GATES, TEST_GATE, WHOLE_FILE, Finding, GateResult, Target

CANARY_DIRECTORY = Path("tests/StyleCanary")
CANARY = Target(
    dotnet_project=CANARY_DIRECTORY / "StyleCanary.csproj",
    csharp_paths=[CANARY_DIRECTORY / "Violations.cs"],
    python_paths=[CANARY_DIRECTORY / "violations.py"],
    shell_paths=[CANARY_DIRECTORY / "violations.sh"],
)
REPOSITORY = Target(
    dotnet_project=Path("AgenticSoftwareWorkflow.slnx"),
    csharp_paths=[],
    python_paths=[],
    shell_paths=[],
)
EXPECT_MARKER = re.compile(r"(?://|#) expect: (?P<gate>[\w-]+):(?P<rule>[\w-]+)")
UNCOVERED_CLASS_PATH = Path("src/AgenticSoftwareWorkflow.Conductor/GateCanaryUncovered.cs")
UNCOVERED_CLASS = (
    "namespace AgenticSoftwareWorkflow.Conductor;\n\n"
    "public static class GateCanaryUncovered\n{\n    public static int Answer() => 42;\n}"
)


def expectations_in(path: Path) -> list[Finding]:
    """Every `expect:` marker in one canary file, as the Finding it demands."""
    lines = (REPOSITORY_ROOT / path).read_text(encoding="utf-8").splitlines()
    return [
        Finding(match["gate"], match["rule"], path.name, number)
        for number, line in enumerate(lines, start=1)
        for match in EXPECT_MARKER.finditer(line)
    ]


def is_reported(expected: Finding, findings: Iterable[Finding]) -> bool:
    """True when a finding matches; a whole-file finding satisfies a marker on any line of that file."""
    return any(
        (found.rule, found.file_name) == (expected.rule, expected.file_name)
        and found.line_number in {expected.line_number, WHOLE_FILE}
        for found in findings
    )


def missing_findings(expectations: Sequence[Finding], results: Sequence[GateResult]) -> list[Finding]:
    """Expectations that no gate of the expected name reported."""
    findings_by_gate = {result.gate: result.findings for result in results}
    return [expected for expected in expectations if not is_reported(expected, findings_by_gate.get(expected.gate, []))]


def verify_static_gates() -> bool:
    """Run each static gate on the canary; every marker must be matched by a finding."""
    canary_files = [*CANARY.csharp_paths, *CANARY.python_paths, *CANARY.shell_paths]
    expectations = [expected for path in canary_files for expected in expectations_in(path)]
    results = [gate.run(CANARY) for gate in STATIC_GATES]
    missing = missing_findings(expectations, results)
    for expected in missing:
        print(f"[MISSED] {expected.gate} did not report {expected.rule} at {expected.file_name}:{expected.line_number}")
    unproven = {gate.name for gate in STATIC_GATES} - {expected.gate for expected in expectations}
    for gate_name in sorted(unproven):
        print(f"[UNPROVEN] no canary marker exercises the '{gate_name}' gate")
    print(f"static gates: {len(expectations) - len(missing)}/{len(expectations)} expected findings reported")
    return not missing and not unproven


def verify_coverage_gate() -> bool:
    """The suite must fail once the library contains code no test covers."""
    uncovered_class = REPOSITORY_ROOT / UNCOVERED_CLASS_PATH
    uncovered_class.write_text(UNCOVERED_CLASS, encoding="utf-8")
    try:
        result = TEST_GATE.run(REPOSITORY)
    finally:
        uncovered_class.unlink()
    print(f"coverage gate: {'caught' if not result.has_passed else 'MISSED'} an uncovered class")
    return not result.has_passed


def main() -> int:
    """Verify every gate; exit 1 if any gate failed to catch its canary."""
    is_static_proven = verify_static_gates()
    is_coverage_proven = verify_coverage_gate()
    return 0 if is_static_proven and is_coverage_proven else 1


if __name__ == "__main__":
    sys.exit(main())
