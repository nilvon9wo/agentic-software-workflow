"""Shared harness for testing the gates without running their tools."""

import subprocess
from collections.abc import Callable, Sequence
from pathlib import Path

from gates.model import Target

CANARY_PROJECT = Path("Canary.csproj")

type FakeExecute = Callable[[Sequence[str]], subprocess.CompletedProcess[str]]


def a_target(
    markdown_paths: Sequence[Path] = (),
    workflow_paths: Sequence[Path] = (),
    dotnet_project: Path = CANARY_PROJECT,
) -> Target:
    """A target holding only the paths a test cares about."""
    return Target(
        dotnet_project=dotnet_project,
        csharp_paths=(),
        python_paths=(),
        shell_paths=(),
        markdown_paths=markdown_paths,
        workflow_paths=workflow_paths,
    )


def a_tool_printing(
    stdout: str,
    exit_code: int = 0,
    stderr: str = "",
) -> FakeExecute:
    """An `execute` stand-in for a tool that prints and exits as told."""

    def fake_execute(
        command: Sequence[str],
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(
            list(command),
            exit_code,
            stdout=stdout,
            stderr=stderr,
        )

    return fake_execute
