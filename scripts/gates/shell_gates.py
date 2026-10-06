"""The shell gate: shellcheck."""

from pathlib import Path

from gates.model import Finding, GateResult, JsonObject, Target
from gates.tools import (
    NO_OUTPUT_JSON_OBJECT,
    as_arguments,
    combined_output,
    execute,
    parse_json,
)


def shellcheck_finding(comment: JsonObject) -> Finding:
    """A Finding from one shellcheck json1 comment."""
    path = Path(comment["file"])
    rule = f"SC{comment['code']}"
    return Finding("shellcheck", rule, path.name, comment["line"])


def run_shellcheck(target: Target) -> GateResult:
    """Shell quoting, portability, and bug patterns."""
    paths = as_arguments(target.shell_paths)
    command = [
        "shellcheck",
        "--format",
        "json1",
        "--external-sources",
        *paths,
    ]
    completed = execute(command)
    report: JsonObject = parse_json(completed.stdout, NO_OUTPUT_JSON_OBJECT)
    comments: list[JsonObject] = report.get("comments", [])
    findings = [shellcheck_finding(comment) for comment in comments]
    output = combined_output(completed)
    return GateResult("shellcheck", completed.returncode, findings, output)
