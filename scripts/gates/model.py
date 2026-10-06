"""What the gates work with: targets, findings, results, and gates.

A gate turns its tool's output into `Finding`s. It fails when the tool
exits non-zero or reports any finding: there is no warning tier to ignore.
"""

from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

WHOLE_FILE = 0

type JsonObject = dict[str, Any]


@dataclass(frozen=True)
class Finding:
    """One rule reported against one line (or `WHOLE_FILE`) of one file."""

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
    markdown_paths: Sequence[Path]
    workflow_paths: Sequence[Path]


@dataclass(frozen=True)
class GateResult:
    """A gate's verdict, its findings, and the raw tool output."""

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
