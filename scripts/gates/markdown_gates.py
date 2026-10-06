"""The Markdown gates: markdownlint and lychee.

Both are given exact files rather than globs, so the target alone decides
what is checked - the canary in `verify`, the repository's docs in `run`.
"""

import re
from pathlib import Path

from gates.model import Finding, GateResult, JsonObject, Target
from gates.tools import (
    NO_OUTPUT_JSON_OBJECT,
    as_arguments,
    combined_output,
    execute,
    parse_json_report,
)

MARKDOWNLINT_VIOLATION = re.compile(
    r"^(?P<path>[^\n]+?\.md):(?P<line>\d+)(?::\d+)? "
    r"(?:error|warning) (?P<rule>MD\d+)/",
    re.MULTILINE,
)
BROKEN_LINK = "broken-link"


def markdownlint_finding(match: re.Match[str]) -> Finding:
    """A Finding from one `file.md:line[:column] error MD000/alias` line."""
    path = Path(match["path"])
    line_number = int(match["line"])
    return Finding("markdownlint", match["rule"], path.name, line_number)


def run_markdownlint(target: Target) -> GateResult:
    """Markdown structure and style, by .markdownlint-cli2.jsonc's rules."""
    paths = as_arguments(target.markdown_paths)
    completed = execute(["markdownlint-cli2", *paths])
    output = combined_output(completed)
    matches = MARKDOWNLINT_VIOLATION.finditer(output)
    findings = [markdownlint_finding(match) for match in matches]
    return GateResult("markdownlint", completed.returncode, findings, output)


def lychee_finding(file_name: str, failure: JsonObject) -> Finding:
    """A Finding from one entry of lychee's JSON `error_map`."""
    path = Path(file_name)
    line_number = failure["span"]["line"]
    return Finding("lychee", BROKEN_LINK, path.name, line_number)


def run_lychee(target: Target) -> GateResult:
    """Relative links and their anchors must resolve.

    Offline: remote URLs are never fetched, so the gate cannot fail because
    some other site is down.
    """
    paths = as_arguments(target.markdown_paths)
    command = [
        "lychee",
        "--offline",
        "--include-fragments",
        "--no-progress",
        "--format",
        "json",
        *paths,
    ]
    completed = execute(command)
    report: JsonObject = parse_json_report(
        completed.stdout,
        NO_OUTPUT_JSON_OBJECT,
    )
    failures_by_file: JsonObject = report.get("error_map", {})
    findings = [
        lychee_finding(file_name, failure)
        for file_name, failures in failures_by_file.items()
        for failure in failures
    ]
    output = combined_output(completed)
    return GateResult("lychee", completed.returncode, findings, output)
