"""Running external tools and reading what they report."""

import json
import shutil
import subprocess
from collections.abc import Sequence
from pathlib import Path
from typing import Any

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
NO_OUTPUT_JSON_LIST = "[]"
NO_OUTPUT_JSON_OBJECT = "{}"
# What a shell reports for a command it cannot find.
COMMAND_NOT_FOUND = 127

type CompletedTool = subprocess.CompletedProcess[str]


def resolve_executable(name: str) -> str:
    """A tool's full path on PATH, else its bare name.

    Windows needs the full path to run `.cmd` shims such as npx.
    """
    resolved = shutil.which(name)
    if resolved is None:
        return name
    else:
        return resolved


def execute(command: Sequence[str]) -> CompletedTool:
    """Run a tool from the repository root, capturing all it prints.

    A missing tool is a failed run, not a crash, so its gate fails and says
    which tool is missing.
    """
    executable = resolve_executable(command[0])
    arguments = [executable, *command[1:]]
    try:
        return subprocess.run(
            arguments,
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
        )
    except FileNotFoundError:
        missing = f"{command[0]}: command not found\n"
        return subprocess.CompletedProcess(
            arguments,
            COMMAND_NOT_FOUND,
            stdout="",
            stderr=missing,
        )


def combined_output(completed: CompletedTool) -> str:
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


def parse_json_report(text: str, empty_document: str) -> Any:  # noqa: ANN401 - JSON is untyped until a gate reads it
    """A tool's JSON report, or an empty one if it printed something else.

    A tool that crashes prints an error instead of its report. Reading that
    as "nothing reported" is safe because the crash also sets the exit code,
    which fails the gate on its own.
    """
    try:
        return parse_json(text, empty_document)
    except json.JSONDecodeError:
        return json.loads(empty_document)
