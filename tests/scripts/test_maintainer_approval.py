"""Tests for scripts/maintainer_approval.py."""

import subprocess
from collections.abc import Sequence
from pathlib import Path

import pytest

import maintainer_approval
from maintainer_approval import PullRequest, Review, Verdict

HEAD = "abc123"
MAINTAINERS = ["maintainer"]


def pull_request(
    files: Sequence[str],
    reviews: Sequence[Review] = (),
    author: str = "repository-bot",
) -> PullRequest:
    """A pull request at commit HEAD."""
    return PullRequest(author=author, head=HEAD, files=files, reviews=reviews)


def test_judge_when_no_governed_path_changes_passes() -> None:
    # Arrange
    change = pull_request(["src/Program.cs", "docs/README.md"])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS)

    # Assert
    assert verdict == Verdict(
        has_passed=True,
        reason="No governed paths changed.",
    )


@pytest.mark.parametrize(
    "path",
    [
        "spec/8.md",
        ".github/workflows/ci.yml",
        ".github/CODEOWNERS",
        "scripts/maintainer_approval.py",
        "aswf.json",
    ],
)
def test_judge_when_a_governed_path_changes_unapproved_fails(
    path: str,
) -> None:
    # Arrange
    change = pull_request([path])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS)

    # Assert
    assert verdict == Verdict(
        has_passed=False,
        reason=(
            f"Changes governed paths ({path}): a maintainer must approve "
            f"commit {HEAD}."
        ),
    )


def test_judge_when_a_maintainer_authored_it_passes() -> None:
    # Arrange
    change = pull_request(["spec/8.md"], author="maintainer")

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS)

    # Assert
    assert verdict == Verdict(
        has_passed=True,
        reason="Authored by maintainer maintainer.",
    )


def test_judge_when_a_maintainer_approved_the_head_commit_passes() -> None:
    # Arrange
    approval = Review(author="maintainer", state="APPROVED", commit=HEAD)
    change = pull_request(["spec/8.md"], [approval])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS)

    # Assert
    assert verdict == Verdict(
        has_passed=True,
        reason=f"Approved by maintainer at {HEAD}.",
    )


def test_judge_when_the_approval_is_of_an_older_commit_fails() -> None:
    # Arrange
    stale = Review(author="maintainer", state="APPROVED", commit="older")
    change = pull_request(["spec/8.md"], [stale])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS)

    # Assert
    assert not verdict.has_passed


def test_judge_when_a_later_review_requests_changes_fails() -> None:
    # Arrange
    approval = Review(author="maintainer", state="APPROVED", commit=HEAD)
    second_thoughts = Review(
        author="maintainer",
        state="CHANGES_REQUESTED",
        commit=HEAD,
    )
    change = pull_request(["spec/8.md"], [approval, second_thoughts])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS)

    # Assert
    assert not verdict.has_passed


def test_judge_when_only_a_non_maintainer_approved_fails() -> None:
    # Arrange
    approval = Review(author="stranger", state="APPROVED", commit=HEAD)
    change = pull_request(["spec/8.md"], [approval])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS)

    # Assert
    assert not verdict.has_passed


def fake_github(
    reviews: str,
    calls: list[list[str]],
) -> maintainer_approval.CommandRunner:
    """A stand-in `gh api` answering with canned output, recording each call."""

    def run(arguments: Sequence[str]) -> str:
        calls.append(list(arguments))
        endpoint = arguments[-3]
        if endpoint.endswith("/files"):
            return "spec/8.md\n"
        elif endpoint.endswith("/reviews"):
            return reviews
        else:
            return f"repository-bot\t{HEAD}\n"

    return run


def write_settings(directory: Path) -> Path:
    """An aswf.json naming one maintainer."""
    settings = directory / "aswf.json"
    settings.write_text('{"maintainers": ["maintainer"]}', encoding="utf-8")
    return settings


def test_main_when_a_governed_change_is_approved_passes(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    # Arrange
    github = fake_github(f"maintainer\tAPPROVED\t{HEAD}\n", [])
    settings = write_settings(tmp_path)

    # Act
    exit_code: int = maintainer_approval.main(
        ["owner/repository", "18"],
        run=github,
        settings_file=settings,
    )

    # Assert
    output = capsys.readouterr().out
    assert (exit_code, output) == (0, f"Approved by maintainer at {HEAD}.\n")


def test_main_when_a_governed_change_is_unreviewed_fails(
    tmp_path: Path,
) -> None:
    # Arrange
    github = fake_github("", [])
    settings = write_settings(tmp_path)

    # Act
    exit_code: int = maintainer_approval.main(
        ["owner/repository", "18"],
        run=github,
        settings_file=settings,
    )

    # Assert
    assert exit_code == 1


def test_main_when_called_reads_the_pull_request_its_files_and_reviews(
    tmp_path: Path,
) -> None:
    # Arrange
    calls: list[list[str]] = []
    github = fake_github("", calls)
    settings = write_settings(tmp_path)

    # Act
    maintainer_approval.main(
        ["owner/repository", "18"],
        run=github,
        settings_file=settings,
    )

    # Assert
    endpoints = [call[-3] for call in calls]
    assert endpoints == [
        "repos/owner/repository/pulls/18",
        "repos/owner/repository/pulls/18/files",
        "repos/owner/repository/pulls/18/reviews",
    ]


def test_read_maintainers_when_called_reads_the_repository_settings() -> None:
    # Act
    maintainers: list[str] = maintainer_approval.read_maintainers(
        maintainer_approval.SETTINGS_FILE,
    )

    # Assert
    assert maintainers == ["nilvon9wo"]


def test_run_gh_when_called_returns_what_gh_printed(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    def fake_run(
        command: list[str],
        **_options: object,
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.CompletedProcess(command, 0, stdout="output")

    monkeypatch.setattr(subprocess, "run", fake_run)

    # Act
    output: str = maintainer_approval.run_gh(["api", "user"])

    # Assert
    assert output == "output"
