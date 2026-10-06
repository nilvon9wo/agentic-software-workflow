"""The GitHub workflow gate: actionlint."""

from pathlib import Path

from gates.model import Finding, GateResult, JsonObject, Target
from gates.tools import (
    NO_OUTPUT_JSON_LIST,
    as_arguments,
    combined_output,
    execute,
    parse_json_report,
)

JSON_FORMAT = "{{json .}}"


def actionlint_finding(error: JsonObject) -> Finding:
    """A Finding from one entry of actionlint's JSON output."""
    path = Path(error["filepath"])
    return Finding("actionlint", error["kind"], path.name, error["line"])


def run_actionlint(target: Target) -> GateResult:
    """Workflow syntax, expressions, and the shell scripts in `run:` steps."""
    paths = as_arguments(target.workflow_paths)
    completed = execute(["actionlint", "-format", JSON_FORMAT, *paths])
    errors: list[JsonObject] = parse_json_report(
        completed.stdout,
        NO_OUTPUT_JSON_LIST,
    )
    findings = [actionlint_finding(error) for error in errors]
    output = combined_output(completed)
    return GateResult("actionlint", completed.returncode, findings, output)
