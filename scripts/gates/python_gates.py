"""The Python gates: ruff, pylint, pyright, and pytest."""

from collections.abc import Sequence
from pathlib import Path

from gates.model import Finding, GateResult, JsonObject, Target
from gates.tools import (
    NO_OUTPUT_JSON_LIST,
    NO_OUTPUT_JSON_OBJECT,
    as_arguments,
    combined_output,
    execute,
    parse_json,
)


def ruff_finding(gate: str, result: JsonObject) -> Finding:
    """A Finding from one entry of ruff's JSON output."""
    path = Path(result["filename"])
    line_number = result["location"]["row"]
    return Finding(gate, result["code"], path.name, line_number)


def run_ruff_gate(
    gate: str,
    ruff_command: Sequence[str],
    target: Target,
) -> GateResult:
    """Run a ruff subcommand; `format` and `check` share a JSON shape."""
    paths = as_arguments(target.python_paths)
    command = [
        "python",
        "-m",
        "ruff",
        *ruff_command,
        "--output-format",
        "json",
        *paths,
    ]
    completed = execute(command)
    results: list[JsonObject] = parse_json(
        completed.stdout,
        NO_OUTPUT_JSON_LIST,
    )
    findings = [ruff_finding(gate, result) for result in results]
    output = combined_output(completed)
    return GateResult(gate, completed.returncode, findings, output)


def run_ruff_format(target: Target) -> GateResult:
    """Python formatting: the counterpart of `dotnet format`."""
    return run_ruff_gate("ruff-format", ["format", "--check"], target)


def run_ruff(target: Target) -> GateResult:
    """Python lint: naming, complexity, docstrings, bug patterns."""
    return run_ruff_gate("ruff", ["check"], target)


def pylint_finding(message: JsonObject) -> Finding:
    """A Finding from one message of pylint's json2 output."""
    path = Path(message["path"])
    return Finding("pylint", message["messageId"], path.name, message["line"])


def run_pylint(target: Target) -> GateResult:
    """The house rules no other linter checks, plus pylint's own."""
    paths = as_arguments(target.python_paths)
    command = [
        "python",
        "-m",
        "pylint",
        "--output-format=json2",
        "--score=n",
        *paths,
    ]
    completed = execute(command)
    report: JsonObject = parse_json(completed.stdout, NO_OUTPUT_JSON_OBJECT)
    messages: list[JsonObject] = report.get("messages", [])
    findings = [pylint_finding(message) for message in messages]
    output = combined_output(completed)
    return GateResult("pylint", completed.returncode, findings, output)


def pyright_finding(diagnostic: JsonObject) -> Finding:
    """A Finding from one pyright diagnostic (pyright counts from zero)."""
    rule = diagnostic.get("rule", diagnostic["severity"])
    path = Path(diagnostic["file"])
    zero_based_line = diagnostic["range"]["start"]["line"]
    return Finding("pyright", rule, path.name, zero_based_line + 1)


def run_pyright(target: Target) -> GateResult:
    """Python static types in strict mode: the compiler counterpart."""
    paths = as_arguments(target.python_paths)
    command = ["python", "-m", "pyright", "--outputjson", *paths]
    completed = execute(command)
    report: JsonObject = parse_json(completed.stdout, NO_OUTPUT_JSON_OBJECT)
    diagnostics: list[JsonObject] = report.get("generalDiagnostics", [])
    findings = [pyright_finding(diagnostic) for diagnostic in diagnostics]
    output = combined_output(completed)
    return GateResult("pyright", completed.returncode, findings, output)


def run_python_tests(_target: Target) -> GateResult:
    """The Python suite.

    Coverage below 100% line or branch fails it; see pyproject.toml.
    """
    command = ["python", "-m", "pytest", "-p", "no:cacheprovider"]
    completed = execute(command)
    output = combined_output(completed)
    return GateResult("pytest", completed.returncode, [], output)
