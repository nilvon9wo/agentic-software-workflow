#!/usr/bin/env python3
"""Run every quality gate against the repository; exit 1 if any fails.

This is THE definition of "done": CI runs it, and every AI worker must pass
it before its work is accepted. Run it locally before pushing.

Usage: run_gates.py [gate ...]   (default: all; names as in the summary)
"""

import sys
from collections.abc import Sequence
from pathlib import Path

from exit_codes import exit_code_for
from gates.model import Gate, GateResult, Target
from gates.registry import PYTHON_TEST_GATE, STATIC_GATES, TEST_GATE
from gates.tools import REPOSITORY_ROOT

SCRIPTS = Path("scripts")
SHELL_SCRIPTS = sorted(SCRIPTS.glob("*.sh"))
REPOSITORY = Target(
    dotnet_project=Path("AgenticSoftwareWorkflow.slnx"),
    csharp_paths=[Path("src"), Path("tests")],
    python_paths=[SCRIPTS, Path("tests/scripts")],
    shell_paths=SHELL_SCRIPTS,
)
ALL_GATES = (*STATIC_GATES, TEST_GATE, PYTHON_TEST_GATE)


def describe_verdict(result: GateResult) -> str:
    """PASS or FAIL."""
    if result.has_passed:
        return "PASS"
    else:
        return "FAIL"


def report(result: GateResult) -> None:
    """Print a one-line verdict, plus the tool output if the gate failed."""
    verdict = describe_verdict(result)
    print(f"[{verdict}] {result.gate}", flush=True)
    if not result.has_passed:
        print(result.output.strip(), flush=True)


def describe_unknown(unknown: list[str], known: list[str]) -> str:
    """The error for gate names that do not exist."""
    unknown_names = ", ".join(unknown)
    known_names = ", ".join(known)
    return f"unknown gate(s): {unknown_names}; known: {known_names}"


def select(names: Sequence[str]) -> list[Gate]:
    """The gates named, or all of them; an unknown name is an error."""
    known = {gate.name: gate for gate in ALL_GATES}
    unknown = sorted(set(names) - known.keys())
    if unknown:
        known_names = list(known)
        raise SystemExit(describe_unknown(unknown, known_names))
    elif names:
        return [known[name] for name in names]
    else:
        return list(ALL_GATES)


def main(arguments: Sequence[str]) -> int:
    """Run the selected gates in order, reporting each.

    Never stops early, so one run shows everything that needs fixing.
    """
    print(f"Running gates in {REPOSITORY_ROOT}", flush=True)
    results: list[GateResult] = []
    for gate in select(arguments):
        result = gate.run(REPOSITORY)
        report(result)
        results.append(result)
    failures = [result for result in results if not result.has_passed]
    has_all_passed = not failures
    return exit_code_for(has_all_passed)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
