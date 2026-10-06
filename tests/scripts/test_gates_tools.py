"""Tests for scripts/gates/tools.py."""

import shutil
import subprocess
from pathlib import Path

import pytest

from gates import tools


def test_resolve_executable_when_on_path_returns_its_full_path(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    def fake_which(_name: str) -> str:
        return "/usr/bin/lychee"

    monkeypatch.setattr(shutil, "which", fake_which)
    resolved: str

    # Act
    resolved = tools.resolve_executable("lychee")

    # Assert
    assert resolved == "/usr/bin/lychee"


def test_resolve_executable_when_not_on_path_returns_its_name(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    def fake_which(_name: str) -> None:
        return None

    monkeypatch.setattr(shutil, "which", fake_which)
    resolved: str

    # Act
    resolved = tools.resolve_executable("lychee")

    # Assert
    assert resolved == "lychee"


def test_execute_when_the_tool_runs_returns_what_it_printed(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    def fake_run(
        command: list[str],
        **_options: object,
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(command, 0, stdout="output")

    monkeypatch.setattr(subprocess, "run", fake_run)
    completed: subprocess.CompletedProcess[str]

    # Act
    completed = tools.execute(["lychee", "--version"])

    # Assert
    assert completed.stdout == "output"


def test_execute_when_the_tool_is_missing_reports_command_not_found(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    def fake_run(
        command: list[str],
        **_options: object,
    ) -> subprocess.CompletedProcess[str]:
        raise FileNotFoundError(command[0])

    monkeypatch.setattr(subprocess, "run", fake_run)
    completed: subprocess.CompletedProcess[str]

    # Act
    completed = tools.execute(["lychee", "--version"])

    # Assert
    assert (completed.returncode, completed.stderr) == (
        tools.COMMAND_NOT_FOUND,
        "lychee: command not found\n",
    )


def test_combined_output_when_called_puts_stdout_first() -> None:
    # Arrange
    completed = subprocess.CompletedProcess(
        ["lychee"],
        0,
        stdout="out\n",
        stderr="err\n",
    )
    output: str

    # Act
    output = tools.combined_output(completed)

    # Assert
    assert output == "out\nerr\n"


def test_as_arguments_when_called_returns_each_path_as_text() -> None:
    # Arrange
    paths = [Path("docs") / "README.md"]
    arguments: list[str]

    # Act
    arguments = tools.as_arguments(paths)

    # Assert
    assert arguments == [str(paths[0])]


def test_parse_json_when_given_a_report_returns_it() -> None:
    # Arrange
    parsed: object

    # Act
    parsed = tools.parse_json('[{"line": 3}]', tools.NO_OUTPUT_JSON_LIST)

    # Assert
    assert parsed == [{"line": 3}]


def test_parse_json_when_given_nothing_returns_the_empty_document() -> None:
    # Arrange
    parsed: object

    # Act
    parsed = tools.parse_json("  \n", tools.NO_OUTPUT_JSON_LIST)

    # Assert
    assert parsed == []


def test_parse_json_report_when_given_a_report_returns_it() -> None:
    # Arrange
    parsed: object

    # Act
    parsed = tools.parse_json_report(
        '{"errors": 1}',
        tools.NO_OUTPUT_JSON_OBJECT,
    )

    # Assert
    assert parsed == {"errors": 1}


def test_parse_json_report_when_given_a_crash_returns_the_empty_document() -> (
    None
):
    # Arrange
    parsed: object

    # Act
    parsed = tools.parse_json_report(
        "panic: no such flag",
        tools.NO_OUTPUT_JSON_OBJECT,
    )

    # Assert
    assert parsed == {}
