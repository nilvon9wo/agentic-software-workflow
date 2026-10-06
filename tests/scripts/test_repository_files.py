"""Tests for scripts/gates/repository_files.py."""

from collections.abc import Sequence
from pathlib import Path

import pytest
from gate_testing import FakeExecute, a_tool_printing

from gates import repository_files as listing

README = Path("README.md")
CANARY = listing.CANARY_DIRECTORY / "violations.md"
CI = listing.WORKFLOW_DIRECTORY / "ci.yml"


def git_listing(tmp_path: Path, paths: Sequence[Path]) -> FakeExecute:
    """A `git ls-files` that lists `paths`, all present under `tmp_path`."""
    for path in paths:
        (tmp_path / path).parent.mkdir(parents=True, exist_ok=True)
        (tmp_path / path).write_text("present\n")
    lines = [f"{path.as_posix()}\n" for path in paths]
    return a_tool_printing("".join(lines))


def test_repository_files_when_git_fails_raises_with_its_output(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    tool = a_tool_printing("", exit_code=128, stderr="not a git repository")
    monkeypatch.setattr(listing, "execute", tool)

    # Act
    with pytest.raises(listing.RepositoryUnlistableError) as raised:
        listing.repository_files("*.md")

    # Assert
    assert str(raised.value) == "not a git repository"


def test_repository_files_when_a_listed_file_is_deleted_skips_it(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    # Arrange
    tool = git_listing(tmp_path, [README])
    monkeypatch.setattr(listing, "execute", tool)
    monkeypatch.setattr(listing, "REPOSITORY_ROOT", tmp_path)
    (tmp_path / README).unlink()

    # Act
    files: list[Path] = listing.repository_files("*.md")

    # Assert
    assert files == []


def test_documents_when_called_leaves_out_the_canary(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    # Arrange
    tool = git_listing(tmp_path, [README, CANARY])
    monkeypatch.setattr(listing, "execute", tool)
    monkeypatch.setattr(listing, "REPOSITORY_ROOT", tmp_path)

    # Act
    documents: list[Path] = listing.documents()

    # Assert
    assert documents == [README]


def test_workflows_when_called_returns_the_listed_workflows(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    # Arrange
    tool = git_listing(tmp_path, [CI])
    monkeypatch.setattr(listing, "execute", tool)
    monkeypatch.setattr(listing, "REPOSITORY_ROOT", tmp_path)

    # Act
    workflows: list[Path] = listing.workflows()

    # Assert
    assert workflows == [CI]
