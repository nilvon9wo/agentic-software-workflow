"""Tests for scripts/maintainer_approval.py."""

import subprocess
from collections.abc import Sequence
from pathlib import Path

import pytest

import maintainer_approval
from maintainer_approval import (
    ChangedFile,
    DiffReader,
    PullRequest,
    Review,
    Verdict,
)

HEAD = "abc123"
EARLIER = "def456"
MAINTAINERS = ["maintainer"]
SPEC = ChangedFile(
    path="spec/8.md",
    status="added",
    previous_path="",
    blob="b10b",
)


def pull_request(
    files: Sequence[str],
    reviews: Sequence[Review] = (),
    author: str = "repository-bot",
) -> PullRequest:
    """A pull request at commit HEAD, against master."""
    return PullRequest(
        author=author,
        head=HEAD,
        base="master",
        files=files,
        reviews=reviews,
    )


def recorded_diffs(
    changes: dict[str, Sequence[ChangedFile]],
    compared: list[str],
) -> DiffReader:
    """A stand-in for GitHub's compare, recording each commit it is asked."""

    def diff_of(commit: str) -> Sequence[ChangedFile]:
        compared.append(commit)
        return changes[commit]

    return diff_of


def no_diffs() -> DiffReader:
    """A compare that must not be called: any call fails the test."""
    return recorded_diffs({}, [])


def approved(commit: str, author: str = "maintainer") -> Review:
    """A review approving the given commit."""
    return Review(author=author, state="APPROVED", commit=commit)


def test_judge_when_no_governed_path_changes_passes() -> None:
    # Arrange
    change = pull_request(["src/Program.cs", "docs/README.md"])

    # Act
    verdict: Verdict = maintainer_approval.judge(
        change,
        MAINTAINERS,
        no_diffs(),
    )

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
    verdict: Verdict = maintainer_approval.judge(
        change,
        MAINTAINERS,
        no_diffs(),
    )

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
    verdict: Verdict = maintainer_approval.judge(
        change,
        MAINTAINERS,
        no_diffs(),
    )

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
    verdict: Verdict = maintainer_approval.judge(
        change,
        MAINTAINERS,
        no_diffs(),
    )

    # Assert
    assert verdict == Verdict(
        has_passed=True,
        reason=f"Approved by maintainer at {HEAD}.",
    )


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
    verdict: Verdict = maintainer_approval.judge(
        change,
        MAINTAINERS,
        no_diffs(),
    )

    # Assert
    assert not verdict.has_passed


def test_judge_when_only_a_non_maintainer_approved_fails() -> None:
    # Arrange
    approval = Review(author="stranger", state="APPROVED", commit=HEAD)
    change = pull_request(["spec/8.md"], [approval])

    # Act
    verdict: Verdict = maintainer_approval.judge(
        change,
        MAINTAINERS,
        no_diffs(),
    )

    # Assert
    assert not verdict.has_passed


def test_judge_when_an_earlier_approval_has_identical_changes_passes() -> None:
    # Arrange
    change = pull_request(["spec/8.md"], [approved(EARLIER)])
    diffs = recorded_diffs({HEAD: [SPEC], EARLIER: [SPEC]}, [])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS, diffs)

    # Assert
    assert verdict == Verdict(
        has_passed=True,
        reason=(
            f"Approved by maintainer at {EARLIER}; its own changes are "
            f"identical at {HEAD}."
        ),
    )


@pytest.mark.parametrize(
    "changed",
    [
        ChangedFile("spec/8.md", "added", "", "e0e0"),
        ChangedFile("spec/9.md", "added", "", "b10b"),
        ChangedFile("spec/8.md", "renamed", "spec/7.md", "b10b"),
    ],
    ids=["content", "path", "status"],
)
def test_judge_when_an_earlier_approvals_changes_differ_fails(
    changed: ChangedFile,
) -> None:
    # Arrange
    change = pull_request(["spec/8.md"], [approved(EARLIER)])
    diffs = recorded_diffs({HEAD: [changed], EARLIER: [SPEC]}, [])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS, diffs)

    # Assert
    assert verdict == Verdict(
        has_passed=False,
        reason=(
            "Changes governed paths (spec/8.md): a maintainer must approve "
            f"commit {HEAD}."
        ),
    )


def test_judge_when_a_file_was_added_since_the_approval_fails() -> None:
    # Arrange
    change = pull_request(["spec/8.md"], [approved(EARLIER)])
    extra = ChangedFile("aswf.json", "modified", "", "a5f")
    diffs = recorded_diffs({HEAD: [SPEC, extra], EARLIER: [SPEC]}, [])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS, diffs)

    # Assert
    assert not verdict.has_passed


def test_judge_when_the_head_itself_is_approved_compares_nothing() -> None:
    # Arrange
    compared: list[str] = []
    change = pull_request(["spec/8.md"], [approved(EARLIER), approved(HEAD)])
    diffs = recorded_diffs({}, compared)

    # Act
    maintainer_approval.judge(change, MAINTAINERS, diffs)

    # Assert
    assert not compared


def test_judge_when_nothing_is_approved_compares_nothing() -> None:
    # Arrange
    compared: list[str] = []
    change = pull_request(["spec/8.md"])
    diffs = recorded_diffs({}, compared)

    # Act
    maintainer_approval.judge(change, MAINTAINERS, diffs)

    # Assert
    assert not compared


def test_judge_when_a_later_review_revokes_an_earlier_approval_fails() -> None:
    # Arrange
    revoked = Review(
        author="maintainer",
        state="CHANGES_REQUESTED",
        commit=EARLIER,
    )
    change = pull_request(["spec/8.md"], [approved(EARLIER), revoked])
    diffs = recorded_diffs({HEAD: [SPEC], EARLIER: [SPEC]}, [])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS, diffs)

    # Assert
    assert not verdict.has_passed


def test_judge_when_a_non_maintainer_approved_identical_changes_fails() -> (
    None
):
    # Arrange
    change = pull_request(["spec/8.md"], [approved(EARLIER, "stranger")])
    diffs = recorded_diffs({HEAD: [SPEC], EARLIER: [SPEC]}, [])

    # Act
    verdict: Verdict = maintainer_approval.judge(change, MAINTAINERS, diffs)

    # Assert
    assert not verdict.has_passed


def test_judge_when_maintainers_approved_one_commit_compares_it_once() -> None:
    # Arrange
    compared: list[str] = []
    reviews = [approved(EARLIER, "maintainer"), approved(EARLIER, "second")]
    change = pull_request(["spec/8.md"], reviews)
    diffs = recorded_diffs({HEAD: [SPEC], EARLIER: [SPEC]}, compared)

    # Act
    maintainer_approval.judge(change, ["maintainer", "second"], diffs)

    # Assert
    assert sorted(compared) == [HEAD, EARLIER]


def test_judge_when_several_earlier_approvals_hold_names_them_all() -> None:
    # Arrange
    reviews = [approved(EARLIER, "maintainer"), approved("older", "second")]
    change = pull_request(["spec/8.md"], reviews)
    changes: dict[str, Sequence[ChangedFile]] = {
        HEAD: [SPEC],
        EARLIER: [SPEC],
        "older": [SPEC],
    }
    diffs = recorded_diffs(changes, [])

    # Act
    verdict: Verdict = maintainer_approval.judge(
        change,
        ["maintainer", "second"],
        diffs,
    )

    # Assert
    assert verdict.reason == (
        f"Approved by maintainer at {EARLIER}, second at older; its own "
        f"changes are identical at {HEAD}."
    )


def test_compare_reader_when_called_reads_the_commits_own_changes() -> None:
    # Arrange
    calls: list[list[str]] = []

    def github(arguments: Sequence[str]) -> str:
        calls.append(list(arguments))
        return "spec/8.md\trenamed\tspec/7.md\tb10b\n"

    diff_of = maintainer_approval.compare_reader("owner/repo", "master", github)

    # Act
    changes = diff_of(HEAD)

    # Assert
    assert (calls[0][1], list(changes)) == (
        f"repos/owner/repo/compare/master...{HEAD}",
        [ChangedFile("spec/8.md", "renamed", "spec/7.md", "b10b")],
    )


def test_compare_reader_when_github_fails_raises() -> None:
    # Arrange
    def github(arguments: Sequence[str]) -> str:
        raise subprocess.CalledProcessError(1, ["gh", *arguments])

    diff_of = maintainer_approval.compare_reader("owner/repo", "master", github)

    # Act
    with pytest.raises(subprocess.CalledProcessError) as raised:
        diff_of(EARLIER)

    # Assert
    assert raised.value.returncode == 1


def fake_github(
    reviews: str,
    calls: list[list[str]],
) -> maintainer_approval.CommandRunner:
    """A stand-in `gh api` answering with canned output, recording each call."""
    answers = [
        ("/files", "spec/8.md\n"),
        ("/reviews", reviews),
        ("/compare/", "spec/8.md\tadded\t\tb10b\n"),
    ]

    def run(arguments: Sequence[str]) -> str:
        calls.append(list(arguments))
        endpoint = arguments[-3]
        matching = [
            answer
            for marker, answer in answers
            if marker in endpoint
        ]
        matching.append(f"repository-bot\t{HEAD}\tmaster\n")
        return matching[0]

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


def test_main_when_only_the_base_was_merged_since_approval_passes(
    tmp_path: Path,
) -> None:
    # Arrange
    github = fake_github(f"maintainer\tAPPROVED\t{EARLIER}\n", [])
    settings = write_settings(tmp_path)

    # Act
    exit_code: int = maintainer_approval.main(
        ["owner/repository", "18"],
        run=github,
        settings_file=settings,
    )

    # Assert
    assert exit_code == 0


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
    # Arrange
    # Nothing to arrange: it reads the repository's real settings file.

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
