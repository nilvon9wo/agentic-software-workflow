"""The .NET gates: build, format, ReSharper inspections, and tests."""

import json
import re
import tempfile
from collections.abc import Sequence
from pathlib import Path

from gates.model import Finding, GateResult, JsonObject, Target
from gates.tools import combined_output, execute

MSBUILD_DIAGNOSTIC = re.compile(
    r"(?P<path>[^\s(:]+\.cs)\((?P<line>\d+),\d+\): "
    r"(?:error|warning) (?P<rule>[A-Z]+\d*)",
)


def msbuild_finding(gate: str, match: re.Match[str]) -> Finding:
    """A Finding from one `File.cs(line,col): error RULE` diagnostic."""
    path = Path(match["path"])
    line_number = int(match["line"])
    return Finding(gate, match["rule"], path.name, line_number)


def finding_order(finding: Finding) -> tuple[str, int, str]:
    """Sort key: file, then line, then rule."""
    return (finding.file_name, finding.line_number, finding.rule)


def msbuild_findings(gate: str, output: str) -> list[Finding]:
    """Findings in file order, de-duplicated.

    MSBuild repeats each diagnostic once per target that reports it.
    """
    matches = MSBUILD_DIAGNOSTIC.finditer(output)
    unique_findings = {msbuild_finding(gate, match) for match in matches}
    return sorted(unique_findings, key=finding_order)


def run_msbuild_gate(gate: str, command: Sequence[str]) -> GateResult:
    """Run a dotnet command whose diagnostics use MSBuild's format."""
    completed = execute(command)
    output = combined_output(completed)
    findings = msbuild_findings(gate, output)
    return GateResult(gate, completed.returncode, findings, output)


def run_build(target: Target) -> GateResult:
    """Compile with every in-build analyzer, .editorconfig style included."""
    project = str(target.dotnet_project)
    command = ["dotnet", "build", project, "-nologo", "--no-incremental"]
    return run_msbuild_gate("build", command)


def run_format(target: Target) -> GateResult:
    """Whitespace, final newline, line endings."""
    project = str(target.dotnet_project)
    command = [
        "dotnet",
        "format",
        project,
        "--verify-no-changes",
        "--severity",
        "info",
    ]
    return run_msbuild_gate("format", command)


def run_tests(target: Target) -> GateResult:
    """The .NET suite.

    Coverage below 100% line or branch fails it; see the test project's
    TestingPlatformCommandLineArguments.
    """
    project = str(target.dotnet_project)
    completed = execute(["dotnet", "test", "--solution", project])
    output = combined_output(completed)
    return GateResult("test", completed.returncode, [], output)


def sarif_finding(result: JsonObject) -> Finding:
    """A Finding from one SARIF result, as inspectcode writes them."""
    location = result["locations"][0]["physicalLocation"]
    uri = location["artifactLocation"]["uri"]
    path = Path(uri)
    line_number = location["region"]["startLine"]
    return Finding("inspect", result["ruleId"], path.name, line_number)


def read_sarif_results(report: Path) -> list[JsonObject]:
    """A SARIF report's results; none if the tool wrote no report."""
    if report.exists():
        text = report.read_text(encoding="utf-8-sig")
        sarif: JsonObject = json.loads(text)
        return sarif["runs"][0]["results"]
    else:
        return []


def inspect_command(target: Target, report: Path) -> list[str]:
    """The inspectcode command line.

    `--no-build` matters: inspectcode otherwise builds first and, when the
    build fails, writes no report at all - which reads as "no findings".
    The build gate owns compilation; this gate only inspects.
    """
    project = str(target.dotnet_project)
    return [
        "dotnet",
        "jb",
        "inspectcode",
        project,
        f"-o={report}",
        "--severity=SUGGESTION",
        "--no-build",
    ]


def run_inspect(target: Target) -> GateResult:
    """ReSharper inspections, down to suggestions.

    They catch what Roslyn misses, such as redundant usings.
    """
    with tempfile.TemporaryDirectory() as directory:
        report = Path(directory) / "inspect.sarif"
        command = inspect_command(target, report)
        completed = execute(command)
        results = read_sarif_results(report)
    findings = [sarif_finding(result) for result in results]
    output = combined_output(completed)
    return GateResult("inspect", completed.returncode, findings, output)
