"""Tests for scripts/gates/snippet_gate.py."""

import subprocess
from collections.abc import Sequence
from pathlib import Path

import pytest
from gate_testing import FakeExecute, a_target

from gates import snippet_gate
from gates.model import WHOLE_FILE, Finding, GateResult

GUIDE = Path("guide.md")
CURRENT = "Shows the tested code.\n"
STALE = "Shows code the tests no longer run.\n"


def mdsnippets_rewriting(document: Path, content: str) -> FakeExecute:
    """An `execute` stand-in for mdsnippets rewriting one document."""

    def fake_execute(
        command: Sequence[str],
    ) -> subprocess.CompletedProcess[str]:
        document.write_text(content)
        return subprocess.CompletedProcess(list(command), 0, "", "")

    return fake_execute


def arrange_guide(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    rewritten_as: str,
) -> Path:
    """A repository holding only GUIDE, which mdsnippets rewrites."""

    def only_the_guide(*_patterns: str) -> list[Path]:
        return [GUIDE]

    guide = tmp_path / GUIDE
    guide.write_text(STALE)
    tool = mdsnippets_rewriting(guide, rewritten_as)
    monkeypatch.setattr(snippet_gate, "execute", tool)
    monkeypatch.setattr(snippet_gate, "REPOSITORY_ROOT", tmp_path)
    monkeypatch.setattr(snippet_gate, "repository_files", only_the_guide)
    return guide


def test_run_snippets_when_a_document_is_stale_reports_it(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    # Arrange
    arrange_guide(monkeypatch, tmp_path, rewritten_as=CURRENT)

    # Act
    result: GateResult = snippet_gate.run_snippets(a_target())

    # Assert
    assert result.findings == [
        Finding("snippets", snippet_gate.STALE_SNIPPET, "guide.md", WHOLE_FILE),
    ]


def test_run_snippets_when_a_document_is_stale_puts_it_back(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    # Arrange
    guide = arrange_guide(monkeypatch, tmp_path, rewritten_as=CURRENT)

    # Act
    snippet_gate.run_snippets(a_target())

    # Assert
    assert guide.read_text() == STALE


def test_run_snippets_when_every_document_is_current_passes(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    # Arrange
    arrange_guide(monkeypatch, tmp_path, rewritten_as=STALE)

    # Act
    result: GateResult = snippet_gate.run_snippets(a_target())

    # Assert
    assert result.has_passed


def test_describe_drift_when_documents_drifted_names_them() -> None:
    # Arrange
    # Nothing to arrange: the input is a literal in the Act.

    # Act
    description: str = snippet_gate.describe_drift([GUIDE])

    # Assert
    assert "  guide.md\n" in description


def test_describe_drift_when_nothing_drifted_is_empty() -> None:
    # Arrange
    # Nothing to arrange: the input is a literal in the Act.

    # Act
    description: str = snippet_gate.describe_drift([])

    # Assert
    assert description == ""
