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

from check_line_layout import LayoutViolation, check_paths

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
WHOLE_FILE = 0
NO_OUTPUT_JSON_LIST = "[]"
NO_OUTPUT_JSON_OBJECT = "{}"
MSBUILD_DIAGNOSTIC = re.compile(
    r"(?P<path>[^\s(:]+\.cs)\((?P<line>\d+),\d+\): (?:error|warning) (?P<rule>[A-Z]+\d*)",
)

type JsonObject = dict[str, Any]


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
        is_clean_exit = self.exit_code == 0
        return is_clean_exit and not self.findings


@dataclass(frozen=True)
class Gate:
    """A named check that can be run against a `Target`."""

    name: str
    run: Callable[[Target], GateResult]


# ----------------------------------------------------------- running tools ---


def resolve_executable(name: str) -> str:
    """The full path of a tool on PATH (Windows needs it for .cmd shims), else the bare name."""
    resolved = shutil.which(name)
    if resolved is None:
        return name
    else:
        return resolved


def execute(command: Sequence[str]) -> subprocess.CompletedProcess[str]:
    """Run a tool from the repository root, capturing everything it prints."""
    executable = resolve_executable(command[0])
    arguments = [executable, *command[1:]]
    return subprocess.run(
        arguments,
        cwd=REPOSITORY_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )


def combined_output(completed: subprocess.CompletedProcess[str]) -> str:
    """Everything a tool printed, stdout first."""
    return completed.stdout + completed.stderr


def as_arguments(paths: Sequence[Path]) -> list[str]:
    """Paths as command-line arguments."""
    return [str(path) for path in paths]


def parse_json(text: str, empty_document: str) -> Any:  # noqa: ANN401 - JSON is untyped until a gate reads it
    """A tool's JSON report; a tool that printed nothing reported nothing."""
    if text.strip():
        return json.loads(text)
    else:
        return json.loads(empty_document)


# ------------------------------------------------------------ .NET gates ----


def msbuild_finding(gate: str, match: re.Match[str]) -> Finding:
    """A Finding from one MSBuild `File.cs(line,col): error RULE` diagnostic."""
    path = Path(match["path"])
    line_number = int(match["line"])
    return Finding(gate, match["rule"], path.name, line_number)


def finding_order(finding: Finding) -> tuple[str, int, str]:
    """Sort key: file, then line, then rule."""
    return (finding.file_name, finding.line_number, finding.rule)


def msbuild_findings(gate: str, output: str) -> list[Finding]:
    """De-duplicated findings (MSBuild repeats diagnostics per target) in file order."""
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
    """Compile with every in-build analyzer (.editorconfig code style included)."""
    project = str(target.dotnet_project)
    command = ["dotnet", "build", project, "-nologo", "--no-incremental"]
    return run_msbuild_gate("build", command)


def run_format(target: Target) -> GateResult:
    """Whitespace, final newline, line endings, and analyzers the build skips."""
    project = str(target.dotnet_project)
    command = ["dotnet", "format", project, "--verify-no-changes", "--severity", "info"]
    return run_msbuild_gate("format", command)


def run_tests(target: Target) -> GateResult:
    """The .NET suite; coverage below 100% line/branch fails it (see the test csproj)."""
    project = str(target.dotnet_project)
    completed = execute(["dotnet", "test", "--solution", project])
    output = combined_output(completed)
    return GateResult("test", completed.returncode, [], output)


def sarif_finding(result: JsonObject) -> Finding:
    """A Finding from one SARIF result, as ReSharper's inspectcode writes them."""
    location = result["locations"][0]["physicalLocation"]
    uri = location["artifactLocation"]["uri"]
    path = Path(uri)
    line_number = location["region"]["startLine"]
    return Finding("inspect", result["ruleId"], path.name, line_number)


def read_sarif_results(report: Path) -> list[JsonObject]:
    """The results of a SARIF report; none if the tool failed before writing one."""
    if report.exists():
        text = report.read_text(encoding="utf-8-sig")
        sarif: JsonObject = json.loads(text)
        return sarif["runs"][0]["results"]
    else:
        return []


def inspect_command(target: Target, report: Path) -> list[str]:
    """The inspectcode command line.

    `--no-build` matters: inspectcode otherwise builds first and, when the build
    fails, writes no report at all - which reads as "no findings". The build
    gate owns compilation; this gate only inspects.
    """
    project = str(target.dotnet_project)
    return ["dotnet", "jb", "inspectcode", project, f"-o={report}", "--severity=SUGGESTION", "--no-build"]


def run_inspect(target: Target) -> GateResult:
    """ReSharper inspections: catches what Roslyn misses (e.g. redundant usings), down to suggestions."""
    with tempfile.TemporaryDirectory() as directory:
        report = Path(directory) / "inspect.sarif"
        command = inspect_command(target, report)
        completed = execute(command)
        results = read_sarif_results(report)
    findings = [sarif_finding(result) for result in results]
    output = combined_output(completed)
    return GateResult("inspect", completed.returncode, findings, output)


def layout_finding(violation: LayoutViolation) -> Finding:
    """A Finding from one layout violation."""
    return Finding("layout", violation.rule, violation.path.name, violation.line_number)


def run_layout(target: Target) -> GateResult:
    """Line length and wrapped-`)` placement (see check_line_layout.py)."""
    roots = list(target.csharp_paths)
    violations = check_paths(roots)
    findings = [layout_finding(violation) for violation in violations]
    descriptions = [violation.describe() for violation in violations]
    output = "\n".join(descriptions)
    return GateResult("layout", 0, findings, output)


# ---------------------------------------------------------- Python gates ----


def ruff_finding(gate: str, result: JsonObject) -> Finding:
    """A Finding from one entry of ruff's JSON output."""
    path = Path(result["filename"])
    line_number = result["location"]["row"]
    return Finding(gate, result["code"], path.name, line_number)


def run_ruff_gate(gate: str, ruff_command: Sequence[str], target: Target) -> GateResult:
    """Run a ruff subcommand with JSON output; `format` and `check` share the same report shape."""
    paths = as_arguments(target.python_paths)
    command = ["python", "-m", "ruff", *ruff_command, "--output-format", "json", *paths]
    completed = execute(command)
    results: list[JsonObject] = parse_json(completed.stdout, NO_OUTPUT_JSON_LIST)
    findings = [ruff_finding(gate, result) for result in results]
    output = combined_output(completed)
    return GateResult(gate, completed.returncode, findings, output)


def run_ruff_format(target: Target) -> GateResult:
    """Python formatting, the counterpart of `dotnet format`'s whitespace check."""
    return run_ruff_gate("ruff-format", ["format", "--check"], target)


def run_ruff(target: Target) -> GateResult:
    """Python lint: naming, complexity, docstrings, bug patterns - the analyzer counterpart."""
    return run_ruff_gate("ruff", ["check"], target)


def pylint_finding(message: JsonObject) -> Finding:
    """A Finding from one message of pylint's json2 output."""
    path = Path(message["path"])
    return Finding("pylint", message["messageId"], path.name, message["line"])


def run_pylint(target: Target) -> GateResult:
    """The house rules no other linter checks (scripts/lint/house_rules.py), plus pylint's own."""
    paths = as_arguments(target.python_paths)
    command = ["python", "-m", "pylint", "--output-format=json2", "--score=n", *paths]
    completed = execute(command)
    report: JsonObject = parse_json(completed.stdout, NO_OUTPUT_JSON_OBJECT)
    messages: list[JsonObject] = report.get("messages", [])
    findings = [pylint_finding(message) for message in messages]
    output = combined_output(completed)
    return GateResult("pylint", completed.returncode, findings, output)


def pyright_finding(diagnostic: JsonObject) -> Finding:
    """A Finding from one pyright diagnostic (pyright lines are zero-based)."""
    rule = diagnostic.get("rule", diagnostic["severity"])
    path = Path(diagnostic["file"])
    zero_based_line = diagnostic["range"]["start"]["line"]
    return Finding("pyright", rule, path.name, zero_based_line + 1)


def run_pyright(target: Target) -> GateResult:
    """Python static types in strict mode - the compiler counterpart."""
    paths = as_arguments(target.python_paths)
    completed = execute(["python", "-m", "pyright", "--outputjson", *paths])
    report: JsonObject = parse_json(completed.stdout, NO_OUTPUT_JSON_OBJECT)
    diagnostics: list[JsonObject] = report.get("generalDiagnostics", [])
    findings = [pyright_finding(diagnostic) for diagnostic in diagnostics]
    output = combined_output(completed)
    return GateResult("pyright", completed.returncode, findings, output)


def run_python_tests(_target: Target) -> GateResult:
    """The Python suite; coverage below 100% line/branch fails it (see pyproject.toml)."""
    completed = execute(["python", "-m", "pytest", "-p", "no:cacheprovider"])
    output = combined_output(completed)
    return GateResult("pytest", completed.returncode, [], output)


# ----------------------------------------------------------- shell gates ----


def shellcheck_finding(comment: JsonObject) -> Finding:
    """A Finding from one shellcheck json1 comment."""
    path = Path(comment["file"])
    rule = f"SC{comment['code']}"
    return Finding("shellcheck", rule, path.name, comment["line"])


def run_shellcheck(target: Target) -> GateResult:
    """Shell scripts: quoting, portability, and bug patterns - shell's analyzer counterpart."""
    paths = as_arguments(target.shell_paths)
    completed = execute(["shellcheck", "--format", "json1", "--external-sources", *paths])
    report: JsonObject = parse_json(completed.stdout, NO_OUTPUT_JSON_OBJECT)
    comments: list[JsonObject] = report.get("comments", [])
    findings = [shellcheck_finding(comment) for comment in comments]
    output = combined_output(completed)
    return GateResult("shellcheck", completed.returncode, findings, output)


STATIC_GATES = (
    Gate("build", run_build),
    Gate("format", run_format),
    Gate("inspect", run_inspect),
    Gate("layout", run_layout),
    Gate("ruff-format", run_ruff_format),
    Gate("ruff", run_ruff),
    Gate("pylint", run_pylint),
    Gate("pyright", run_pyright),
    Gate("shellcheck", run_shellcheck),
)
TEST_GATE = Gate("test", run_tests)
PYTHON_TEST_GATE = Gate("pytest", run_python_tests)
