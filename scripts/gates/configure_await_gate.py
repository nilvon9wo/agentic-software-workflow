"""The ConfigureAwait gate: every project weaves and suppresses the same way.

ConfigureAwait.Fody applies `ConfigureAwait(false)` after compilation, so no
analyzer can see it. This gate is what keeps each project declaring it: the
package reference, the assembly attribute and both analyzer suppressions
(with a real justification) in the project's `GlobalSuppressions.cs`.
"""

import re
from pathlib import Path

from gates.model import WHOLE_FILE, Finding, GateResult, Target
from gates.tools import REPOSITORY_ROOT

GATE = "configure-await"
CANARY_DIRECTORY = Path("tests/StyleCanary")
SUPPRESSIONS_FILE = "GlobalSuppressions.cs"
PACKAGES_FILE = "Directory.Packages.props"
EDITORCONFIG_FILE = ".editorconfig"
BUILD_PROPS_FILE = "Directory.Build.props"
FODY_PACKAGE = "ConfigureAwait.Fody"
PACKAGE_REFERENCE = re.compile(
    r'<PackageReference\s+Include="ConfigureAwait\.Fody"'
    r'[^>]*PrivateAssets="all"',
)
PACKAGE_VERSION = re.compile(
    r'<PackageVersion\s+Include="ConfigureAwait\.Fody"'
    r'\s+Version="(?P<version>[^"]*)"',
)
EXACT_VERSION = re.compile(r"\d+(\.\d+)*(-[0-9A-Za-z.-]+)?")
ATTRIBUTE = re.compile(r"\[assembly:\s*Fody\.ConfigureAwait\(false\)\]")
ENFORCER_SUPPRESSION = re.compile(
    r'\[assembly:\s*SuppressMessage\(\s*"ConfigureAwait",'
    r'\s*"ConfigureAwaitEnforcer:ConfigureAwaitEnforcer"'
    r"(?P<rest>.*?)\)\]",
    re.DOTALL,
)
CA2007_SUPPRESSION = re.compile(
    r'\[assembly:\s*SuppressMessage\(\s*"Usage",\s*"CA2007"'
    r"(?P<rest>.*?)\)\]",
    re.DOTALL,
)
STRING_LITERAL = re.compile(r'"([^"]*)"')
EDITORCONFIG_SUPPRESSION = re.compile(
    r"dotnet_diagnostic\.(ConfigureAwaitEnforcer|CA2007)\.severity",
)


def finding(file_name: str, rule: str) -> Finding:
    """A Finding against a whole file."""
    return Finding(GATE, rule, file_name, WHOLE_FILE)


def read(path: Path) -> str:
    """A text file's contents."""
    return path.read_text(encoding="utf-8")


def is_canary(target: Target) -> bool:
    """True when the gate is run against the canary, not the repository."""
    return target.dotnet_project.parent == CANARY_DIRECTORY


def is_in_canary(project: Path) -> bool:
    """True for a project that lives under the canary directory."""
    relative = project.relative_to(REPOSITORY_ROOT)
    return CANARY_DIRECTORY in relative.parents


def project_files(target: Target) -> list[Path]:
    """The projects to check: the real ones, or the canary's own."""
    if is_canary(target):
        canary_root = REPOSITORY_ROOT / CANARY_DIRECTORY
        canary_project = REPOSITORY_ROOT / target.dotnet_project
        projects = canary_root.rglob("*.csproj")
        return [path for path in projects if path != canary_project]
    else:
        projects = [
            *REPOSITORY_ROOT.joinpath("src").rglob("*.csproj"),
            *REPOSITORY_ROOT.joinpath("tests").rglob("*.csproj"),
        ]
        return [path for path in projects if not is_in_canary(path)]


def justification_of(match: re.Match[str]) -> str:
    """The text of the string literals after a suppression's check id."""
    literals = STRING_LITERAL.findall(match["rest"])
    return " ".join(literals)


def is_justified(match: re.Match[str]) -> bool:
    """True when a suppression says what ConfigureAwait.Fody does."""
    return FODY_PACKAGE in justification_of(match)


def suppression_findings(name: str, suppressions: str) -> list[Finding]:
    """What is missing from a project's GlobalSuppressions.cs."""
    findings: list[Finding] = []
    if ATTRIBUTE.search(suppressions) is None:
        findings.append(finding(name, "fody-attribute"))
    enforcer = ENFORCER_SUPPRESSION.search(suppressions)
    if enforcer is None:
        findings.append(finding(name, "suppress-enforcer"))
    ca2007 = CA2007_SUPPRESSION.search(suppressions)
    if ca2007 is None:
        findings.append(finding(name, "suppress-ca2007"))
    matches = (enforcer, ca2007)
    present = [match for match in matches if match is not None]
    justified = [is_justified(match) for match in present]
    if not all(justified):
        findings.append(finding(name, "justification"))
    return findings


def project_findings(project: Path, build_props: str) -> list[Finding]:
    """What is missing from one project."""
    findings: list[Finding] = []
    references = read(project) + build_props
    if PACKAGE_REFERENCE.search(references) is None:
        findings.append(finding(project.name, "package-reference"))
    suppressions_path = project.parent / SUPPRESSIONS_FILE
    if suppressions_path.exists():
        suppressions = read(suppressions_path)
        findings.extend(suppression_findings(project.name, suppressions))
    else:
        findings.append(finding(project.name, "global-suppressions"))
    return findings


def build_props_text(target: Target) -> str:
    """The shared Directory.Build.props the projects inherit.

    The canary is judged on its own project files: the shared props would
    hide the reference its project deliberately omits.
    """
    path = REPOSITORY_ROOT / BUILD_PROPS_FILE
    if path.exists() and not is_canary(target):
        return read(path)
    else:
        return ""


def package_findings() -> list[Finding]:
    """Findings unless the package is pinned once, to an exact version."""
    text = read(REPOSITORY_ROOT / PACKAGES_FILE)
    versions = [match["version"] for match in PACKAGE_VERSION.finditer(text)]
    is_pinned_once = len(versions) == 1
    if is_pinned_once and EXACT_VERSION.fullmatch(versions[0]):
        return []
    else:
        return [finding(PACKAGES_FILE, "package-version")]


def editorconfig_findings() -> list[Finding]:
    """Findings if .editorconfig suppresses a ConfigureAwait rule."""
    text = read(REPOSITORY_ROOT / EDITORCONFIG_FILE)
    if EDITORCONFIG_SUPPRESSION.search(text) is None:
        return []
    else:
        return [finding(EDITORCONFIG_FILE, "editorconfig-suppression")]


def describe(found: Finding) -> str:
    """One line naming a file and what is wrong with it."""
    return f"{found.file_name}: {found.rule}"


def run_configure_await(target: Target) -> GateResult:
    """Every project has the Fody reference, attribute and suppressions."""
    build_props = build_props_text(target)
    findings = [*package_findings(), *editorconfig_findings()]
    for project in project_files(target):
        findings.extend(project_findings(project, build_props))
    lines = [describe(found) for found in findings]
    output = "\n".join(lines)
    return GateResult(GATE, 0, findings, output)
