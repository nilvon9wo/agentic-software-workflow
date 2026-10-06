"""The quality gates, defined once.

`run_gates.py` runs them against the real code (locally, in CI, and as the
deterministic check every AI worker must pass). `verify_gates.py` runs them
against tests/StyleCanary and proves each one still reports what it should.
One definition means "the gates passed" can never mean two different things.

Every gate turns its tool's output into `Finding`s. A gate fails when its tool
exits non-zero or reports any finding: there is no warning tier to ignore.
"""

import json
import re
import shutil
import subprocess
import tempfile
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from check_line_layout import check_paths

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
WHOLE_FILE = 0
MSBUILD_DIAGNOSTIC = re.compile(r"(?P<path>[^\s(:]+\.cs)\((?P<line>\d+),\d+\): (?:error|warning) (?P<rule>[A-Z]+\d*)")


@dataclass(frozen=True)
class Finding:
    """One rule a gate reported against one line (or `WHOLE_FILE`) of one file."""

    gate: str
    rule: str
    file_name: str
    line_number: int


@dataclass(frozen=True)
class Target:
    """What the gates run against: the real repository, or the canary."""

    dotnet_project: Path
    csharp_paths: Sequence[Path]
    python_paths: Sequence[Path]
    shell_paths: Sequence[Path]


@dataclass(frozen=True)
class GateResult:
    """A gate's verdict, its findings, and the raw tool output for diagnosis."""

    gate: str
    exit_code: int
    findings: Sequence[Finding]
    output: str

    @property
    def has_passed(self) -> bool:
        """Passing means a clean exit AND nothing reported."""
        return self.exit_code == 0 and not self.findings


@dataclass(frozen=True)
class Gate:
    """A named check that can be run against a `Target`."""

    name: str
    run: Callable[[Target], GateResult]


def execute(command: Sequence[str]) -> subprocess.CompletedProcess[str]:
    """Run a tool from the repository root, capturing everything it prints."""
    executable = shutil.which(command[0]) or command[0]
    return subprocess.run(
        [executable, *command[1:]],
        cwd=REPOSITORY_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )


def msbuild_findings(gate: str, output: str) -> list[Finding]:
    """Findings from MSBuild-style `File.cs(line,col): error RULE` lines, de-duplicated."""
    matches = MSBUILD_DIAGNOSTIC.finditer(output)
    unique = {Finding(gate, match["rule"], Path(match["path"]).name, int(match["line"])) for match in matches}
    return sorted(unique, key=lambda finding: (finding.file_name, finding.line_number, finding.rule))


def run_msbuild_gate(gate: str, command: Sequence[str]) -> GateResult:
    """Run a dotnet command whose diagnostics use MSBuild's format."""
    completed = execute(command)
    output = completed.stdout + completed.stderr
    return GateResult(gate, completed.returncode, msbuild_findings(gate, output), output)


def run_build(target: Target) -> GateResult:
    """Compile with every in-build analyzer (.editorconfig code style included)."""
    return run_msbuild_gate("build", ["dotnet", "build", str(target.dotnet_project), "-nologo", "--no-incremental"])


def run_format(target: Target) -> GateResult:
    """Whitespace, final newline, line endings, and analyzers the build skips."""
    command = ["dotnet", "format", str(target.dotnet_project), "--verify-no-changes", "--severity", "info"]
    return run_msbuild_gate("format", command)


def run_tests(target: Target) -> GateResult:
    """The test suite; coverage below 100% line/branch fails it (see the test csproj)."""
    completed = execute(["dotnet", "test", "--solution", str(target.dotnet_project)])
    return GateResult("test", completed.returncode, [], completed.stdout + completed.stderr)


def sarif_finding(result: dict[str, Any]) -> Finding:
    """A Finding from one SARIF result, as ReSharper's inspectcode writes them."""
    location = result["locations"][0]["physicalLocation"]
    file_name = Path(location["artifactLocation"]["uri"]).name
    return Finding("inspect", result["ruleId"], file_name, location["region"]["startLine"])


def read_sarif_results(report: Path) -> list[dict[str, Any]]:
    """The results of a SARIF report; none if the tool failed before writing one."""
    if not report.exists():
        return []
    sarif: dict[str, Any] = json.loads(report.read_text(encoding="utf-8-sig"))
    return sarif["runs"][0]["results"]


def run_inspect(target: Target) -> GateResult:
    """ReSharper inspections: catches what Roslyn misses (e.g. redundant usings), down to suggestions.

    `--no-build` matters: inspectcode otherwise builds first and, when the build
    fails, writes no report at all - which reads as "no findings". The build gate
    owns compilation; this gate only inspects.
    """
    with tempfile.TemporaryDirectory() as directory:
        report = Path(directory) / "inspect.sarif"
        command = [
            "dotnet",
            "jb",
            "inspectcode",
            str(target.dotnet_project),
            f"-o={report}",
            "--severity=SUGGESTION",
            "--no-build",
        ]
        completed = execute(command)
        results = read_sarif_results(report)
    findings = [sarif_finding(result) for result in results]
    return GateResult("inspect", completed.returncode, findings, completed.stdout + completed.stderr)


def run_layout(target: Target) -> GateResult:
    """Line length and wrapped-`)` placement (see check_line_layout.py)."""
    violations = check_paths(list(target.csharp_paths))
    findings = [Finding("layout", item.rule, item.path.name, item.line_number) for item in violations]
    return GateResult("layout", 0, findings, "\n".join(item.describe() for item in violations))


def run_ruff_gate(gate: str, ruff_command: Sequence[str], target: Target) -> GateResult:
    """Run a ruff subcommand with JSON output; `format` and `check` share the same report shape."""
    command = ["python", "-m", "ruff", *ruff_command, "--output-format", "json", *map(str, target.python_paths)]
    completed = execute(command)
    results: list[dict[str, Any]] = json.loads(completed.stdout or "[]")
    findings = [
        Finding(gate, result["code"], Path(result["filename"]).name, result["location"]["row"]) for result in results
    ]
    return GateResult(gate, completed.returncode, findings, completed.stdout + completed.stderr)


def run_ruff_format(target: Target) -> GateResult:
    """Python formatting, the counterpart of `dotnet format`'s whitespace check."""
    return run_ruff_gate("ruff-format", ["format", "--check"], target)


def run_ruff(target: Target) -> GateResult:
    """Python lint: naming, complexity, docstrings, bug patterns - the analyzer counterpart."""
    return run_ruff_gate("ruff", ["check"], target)


def pyright_finding(diagnostic: dict[str, Any]) -> Finding:
    """A Finding from one pyright diagnostic (pyright lines are zero-based)."""
    rule = diagnostic.get("rule", diagnostic["severity"])
    line_number = diagnostic["range"]["start"]["line"] + 1
    return Finding("pyright", rule, Path(diagnostic["file"]).name, line_number)


def run_pyright(target: Target) -> GateResult:
    """Python static types in strict mode - the compiler counterpart."""
    command = ["python", "-m", "pyright", "--outputjson", *map(str, target.python_paths)]
    completed = execute(command)
    report: dict[str, Any] = json.loads(completed.stdout or "{}")
    findings = [pyright_finding(diagnostic) for diagnostic in report.get("generalDiagnostics", [])]
    return GateResult("pyright", completed.returncode, findings, completed.stdout + completed.stderr)


def run_shellcheck(target: Target) -> GateResult:
    """Shell scripts: quoting, portability, and bug patterns - shell's analyzer counterpart."""
    completed = execute(["shellcheck", "--format", "json1", "--external-sources", *map(str, target.shell_paths)])
    report: dict[str, Any] = json.loads(completed.stdout or "{}")
    comments: list[dict[str, Any]] = report.get("comments", [])
    findings = [
        Finding("shellcheck", f"SC{comment['code']}", Path(comment["file"]).name, comment["line"])
        for comment in comments
    ]
    return GateResult("shellcheck", completed.returncode, findings, completed.stdout + completed.stderr)


STATIC_GATES = (
    Gate("build", run_build),
    Gate("format", run_format),
    Gate("inspect", run_inspect),
    Gate("layout", run_layout),
    Gate("ruff-format", run_ruff_format),
    Gate("ruff", run_ruff),
    Gate("pyright", run_pyright),
    Gate("shellcheck", run_shellcheck),
)
TEST_GATE = Gate("test", run_tests)
