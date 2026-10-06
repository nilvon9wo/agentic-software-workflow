"""The C# layout gate: line length and wrapped `)` placement."""

from check_line_layout import LayoutViolation, check_paths
from gates.model import Finding, GateResult, Target


def layout_finding(violation: LayoutViolation) -> Finding:
    """A Finding from one layout violation."""
    file_name = violation.path.name
    return Finding("layout", violation.rule, file_name, violation.line_number)


def run_layout(target: Target) -> GateResult:
    """The rules no Roslyn analyzer checks (see check_line_layout.py)."""
    roots = list(target.csharp_paths)
    violations = check_paths(roots)
    findings = [layout_finding(violation) for violation in violations]
    descriptions = [violation.describe() for violation in violations]
    output = "\n".join(descriptions)
    return GateResult("layout", 0, findings, output)
