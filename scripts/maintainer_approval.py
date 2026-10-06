#!/usr/bin/env python3
"""Require a maintainer's approval for pull requests that change governance.

A specification defines what tests and code are held to; the workflows,
this check, and the list of maintainers define how the repository is
governed. A pull request that changes any of these passes only when a
maintainer authored it or approved its current head commit. Every other
pull request passes at once, so ordinary changes still merge on green gates.

GitHub's own "require review from Code Owners" does not enforce anything
while a ruleset requires zero approvals, and requiring approvals on every
pull request would stop the workers' changes from ever merging unattended -
hence this check. See docs/how-it-works/quality-gates.md for its reasons,
risks, and limitations.

Runs from the base branch (pull_request_target): the pull request cannot
change the rules it is judged by, and its code is never checked out.

Usage: maintainer_approval.py <owner/repository> <pull-request-number>
"""

import json
import subprocess
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path

from exit_codes import exit_code_for

GOVERNED_PATHS = (
    "spec/",
    ".github/",
    "scripts/maintainer_approval.py",
    "aswf.json",
)
APPROVED = "APPROVED"
SETTINGS_FILE = Path(__file__).resolve().parent.parent / "aswf.json"
FIELD_SEPARATOR = "\t"

type CommandRunner = Callable[[Sequence[str]], str]


@dataclass(frozen=True)
class Review:
    """One review: who, what they decided, and on which commit."""

    author: str
    state: str
    commit: str


@dataclass(frozen=True)
class PullRequest:
    """What the check needs to know about a pull request."""

    author: str
    head: str
    files: Sequence[str]
    reviews: Sequence[Review]


@dataclass(frozen=True)
class Verdict:
    """Whether the pull request may merge, and why."""

    has_passed: bool
    reason: str


def is_governed(path: str) -> bool:
    """True for a path whose changes need a maintainer's approval."""
    return path.startswith(GOVERNED_PATHS)


def latest_decisions(reviews: Sequence[Review]) -> dict[str, Review]:
    """Each reviewer's latest review; GitHub lists reviews oldest first."""
    return {review.author: review for review in reviews}


def approves(
    review: Review,
    head: str,
    maintainers: Sequence[str],
) -> bool:
    """True when a maintainer approved exactly this commit."""
    return (
        review.author in maintainers
        and review.state == APPROVED
        and review.commit == head
    )


def approvers(
    pull_request: PullRequest,
    maintainers: Sequence[str],
) -> list[str]:
    """Maintainers whose latest review approves the current head commit."""
    decisions = latest_decisions(pull_request.reviews).values()
    return [
        review.author
        for review in decisions
        if approves(review, pull_request.head, maintainers)
    ]


def reasons_to_pass(
    pull_request: PullRequest,
    maintainers: Sequence[str],
    governed: Sequence[str],
) -> list[str]:
    """Every reason the pull request may merge; it may if there is any."""
    approved_by = approvers(pull_request, maintainers)
    names = ", ".join(approved_by)
    candidates = [
        (not governed, "No governed paths changed."),
        (
            pull_request.author in maintainers,
            f"Authored by maintainer {pull_request.author}.",
        ),
        (
            bool(approved_by),
            f"Approved by {names} at {pull_request.head}.",
        ),
    ]
    return [reason for applies, reason in candidates if applies]


def describe_missing_approval(
    pull_request: PullRequest,
    governed: Sequence[str],
) -> str:
    """Why the pull request may not merge yet, and what would let it."""
    paths = ", ".join(governed)
    return (
        f"Changes governed paths ({paths}): a maintainer must approve "
        f"commit {pull_request.head}."
    )


def judge(pull_request: PullRequest, maintainers: Sequence[str]) -> Verdict:
    """Decide whether the pull request may merge."""
    governed = [path for path in pull_request.files if is_governed(path)]
    reasons = reasons_to_pass(pull_request, maintainers, governed)
    if reasons:
        return Verdict(has_passed=True, reason=reasons[0])
    else:
        reason = describe_missing_approval(pull_request, governed)
        return Verdict(has_passed=False, reason=reason)


def run_gh(arguments: Sequence[str]) -> str:
    """Run the GitHub CLI and return its output; a failure raises loudly."""
    completed = subprocess.run(
        ["gh", *arguments],
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=True,
    )
    return completed.stdout


def to_review(line: str) -> Review:
    """A review from one tab-separated line of `gh api` output."""
    author, state, commit = line.split(FIELD_SEPARATOR)
    return Review(author=author, state=state, commit=commit)


def fetch(
    repository: str,
    number: str,
    run: CommandRunner,
) -> PullRequest:
    """The pull request's author, head commit, changed files, and reviews."""
    base = f"repos/{repository}/pulls/{number}"
    summary = run(["api", base, "--jq", "[.user.login, .head.sha] | @tsv"])
    author, head = summary.strip().split(FIELD_SEPARATOR)
    files = run(["api", "--paginate", f"{base}/files", "--jq", ".[].filename"])
    reviews = run(
        [
            "api",
            "--paginate",
            f"{base}/reviews",
            "--jq",
            ".[] | [.user.login, .state, .commit_id] | @tsv",
        ],
    )
    review_list = [to_review(line) for line in reviews.splitlines()]
    return PullRequest(
        author=author,
        head=head,
        files=files.split(),
        reviews=review_list,
    )


def read_maintainers(settings_file: Path) -> list[str]:
    """The maintainers named in aswf.json."""
    settings = json.loads(settings_file.read_text(encoding="utf-8"))
    maintainers: list[str] = settings["maintainers"]
    return maintainers


def main(
    arguments: Sequence[str],
    run: CommandRunner = run_gh,
    settings_file: Path = SETTINGS_FILE,
) -> int:
    """Judge one pull request; exit 1 if it needs an approval it lacks."""
    repository, number = arguments
    pull_request = fetch(repository, number, run)
    maintainers = read_maintainers(settings_file)
    verdict = judge(pull_request, maintainers)
    print(verdict.reason)
    return exit_code_for(verdict.has_passed)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
