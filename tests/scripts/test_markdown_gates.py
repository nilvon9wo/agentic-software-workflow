"""Tests for scripts/gates/markdown_gates.py."""

import json
from pathlib import Path

import pytest
from gate_testing import a_target, a_tool_printing

from gates import markdown_gates
from gates.model import Finding, GateResult

README = Path("docs") / "README.md"
MARKDOWNLINT_OUTPUT = (
    "markdownlint-cli2 v0.23.2\n"
    "docs/README.md:6:5 error MD034/no-bare-urls Bare URL used\n"
    "docs/README.md:9 error MD012/no-multiple-blanks Multiple blanks\n"
)
LYCHEE_REPORT = {
    "error_map": {
        "docs/README.md": [
            {"url": "file:///no-such-file.md", "span": {"line": 8}},
        ],
    },
}


def test_run_markdownlint_when_rules_are_broken_reports_each_line(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    tool = a_tool_printing(MARKDOWNLINT_OUTPUT, exit_code=1)
    monkeypatch.setattr(markdown_gates, "execute", tool)

    # Act
    result: GateResult = markdown_gates.run_markdownlint(
        a_target(markdown_paths=[README]),
    )

    # Assert
    assert result.findings == [
        Finding("markdownlint", "MD034", "README.md", 6),
        Finding("markdownlint", "MD012", "README.md", 9),
    ]


def test_run_lychee_when_a_link_is_broken_reports_its_line(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    report = json.dumps(LYCHEE_REPORT)
    monkeypatch.setattr(markdown_gates, "execute", a_tool_printing(report))

    # Act
    result: GateResult = markdown_gates.run_lychee(
        a_target(markdown_paths=[README]),
    )

    # Assert
    assert result.findings == [
        Finding("lychee", markdown_gates.BROKEN_LINK, "README.md", 8),
    ]


def test_run_lychee_when_every_link_resolves_reports_nothing(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    report = json.dumps({"error_map": {}})
    monkeypatch.setattr(markdown_gates, "execute", a_tool_printing(report))

    # Act
    result: GateResult = markdown_gates.run_lychee(
        a_target(markdown_paths=[README]),
    )

    # Assert
    assert result.has_passed
