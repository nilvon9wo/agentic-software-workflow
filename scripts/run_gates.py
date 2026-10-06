#!/usr/bin/env python3
"""Run every quality gate against the repository; exit 1 if any fails.

This is THE definition of "done": CI runs it, and every AI worker must pass it
before its work is accepted. Run it locally before pushing.

Usage: run_gates.py [gate ...]   (default: all; names as printed in the summary)
"""

import sys
from collections.abc import Sequence
from pathlib import Path

from exit_codes import exit_code_for
from gates import PYTHON_TEST_GATE, REPOSITORY_ROOT, STATIC_GATES, TEST_GATE, Gate, GateResult, Target

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
    """Print a one-line verdict, plus the tool output whenever the gate failed."""
    verdict = describe_verdict(result)
    print(f"[{verdict}] {result.gate}", flush=True)
    if not result.has_passed:
        print(result.output.strip(), flush=True)


def select(names: Sequence[str]) -> list[Gate]:
    """The gates named on the command line, or all of them; unknown names are an error."""
    known = {gate.name: gate for gate in ALL_GATES}
    unknown = sorted(set(names) - known.keys())
    if unknown:
        message = f"unknown gate(s): {', '.join(unknown)}; known: {', '.join(known)}"
        raise SystemExit(message)
    elif names:
        return [known[name] for name in names]
    else:
        return list(ALL_GATES)


def main(arguments: Sequence[str]) -> int:
    """Run the selected gates in order, reporting each; never stop early, so one run shows everything."""
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
