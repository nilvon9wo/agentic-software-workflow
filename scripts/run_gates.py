#!/usr/bin/env python3
"""Run every quality gate against the repository; exit 1 if any fails.

This is THE definition of "done": CI runs it, and every AI worker must pass it
before its work is accepted. Run it locally before pushing.

Usage: run_gates.py [gate ...]   (default: all; names as printed in the summary)
"""

import sys
from pathlib import Path

from gates import REPOSITORY_ROOT, STATIC_GATES, TEST_GATE, Gate, GateResult, Target

REPOSITORY = Target(
    dotnet_project=Path("AgenticSoftwareWorkflow.slnx"),
    csharp_paths=[Path("src"), Path("tests")],
    python_paths=[Path("scripts")],
    shell_paths=sorted(Path("scripts").glob("*.sh")),
)
ALL_GATES = (*STATIC_GATES, TEST_GATE)


def run_and_report(gate: Gate) -> GateResult:
    """Run one gate and print its one-line verdict, plus the tool output whenever it failed."""
    result = gate.run(REPOSITORY)
    verdict = "PASS" if result.has_passed else "FAIL"
    print(f"[{verdict}] {result.gate}", flush=True)
    if not result.has_passed:
        print(result.output.strip(), flush=True)
    return result


def select(names: list[str]) -> list[Gate]:
    """The gates named on the command line, or all of them; unknown names are an error."""
    known = {gate.name: gate for gate in ALL_GATES}
    unknown = sorted(set(names) - known.keys())
    if unknown:
        message = f"unknown gate(s): {', '.join(unknown)}; known: {', '.join(known)}"
        raise SystemExit(message)
    return [known[name] for name in names] if names else list(ALL_GATES)


def main(arguments: list[str]) -> int:
    """Run the selected gates in order, reporting each; never stop early, so one run shows everything."""
    print(f"Running gates in {REPOSITORY_ROOT}", flush=True)
    results = [run_and_report(gate) for gate in select(arguments)]
    return 0 if all(result.has_passed for result in results) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
