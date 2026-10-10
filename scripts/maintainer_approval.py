#!/usr/bin/env python3
"""Require a maintainer's approval for pull requests that change governance.

A specification defines what tests and code are held to; the workflows,
this check, and the list of maintainers define how the repository is
governed. A pull request that changes any of these passes only when a
maintainer authored it or approved its current head commit - or approved
an earlier commit whose own changes (its diff against the base branch) are
identical to the head's, so bringing a pull request up to date does not
void its approval. Every other pull request passes at once, so ordinary
changes still merge on green gates.

Everything the check relies on stays in this one file: only this file is
governed, so a helper module would let a pull request change the rules
without a maintainer's approval.

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
COMPARED_FILE_FIELDS = (
    '.files[] | [.filename, .status, (.previous_filename // ""),'
    ' (.sha // "")] | @tsv'
)

type CommandRunner = Callable[[Sequence[str]], str]


@dataclass(frozen=True)
class ChangedFile:
    """One file in a pull request's own changes, as GitHub compares it.

    The blob SHA stands for the file's exact content, so binary and large
    files, whose patches GitHub omits, are compared as surely as text.
    """

    path: str
    status: str
    previous_path: str
    blob: str


type DiffReader = Callable[[str], Sequence[ChangedFile]]


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
    base: str
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


def is_maintainers_approval(
    review: Review,
    maintainers: Sequence[str],
) -> bool:
    """True when the review is a maintainer's approval, of any commit."""
    return review.author in maintainers and review.state == APPROVED


def maintainers_approvals(
    pull_request: PullRequest,
    maintainers: Sequence[str],
) -> list[Review]:
    """Maintainers' latest reviews that approve; a later verdict revokes."""
    decisions = latest_decisions(pull_request.reviews).values()
    return [
        review
        for review in decisions
        if is_maintainers_approval(review, maintainers)
    ]


def approvers(
    pull_request: PullRequest,
    maintainers: Sequence[str],
) -> list[str]:
    """Maintainers whose latest review approves the current head commit."""
    approvals = maintainers_approvals(pull_request, maintainers)
    return [
        review.author
        for review in approvals
        if review.commit == pull_request.head
    ]


def carried_approvals(
    pull_request: PullRequest,
    maintainers: Sequence[str],
    diff_of: DiffReader,
) -> list[Review]:
    """Approvals of earlier commits whose own changes match the head's.

    Each earlier commit is compared once, and nothing is compared when no
    maintainer approved an earlier commit.
    """
    approvals = maintainers_approvals(pull_request, maintainers)
    earlier = [
        review
        for review in approvals
        if review.commit != pull_request.head
    ]
    if earlier:
        head_changes = diff_of(pull_request.head)
        commits = {review.commit for review in earlier}
        unchanged = {
            commit
            for commit in commits
            if diff_of(commit) == head_changes
        }
        return [review for review in earlier if review.commit in unchanged]
    else:
        return []


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


def describe_carried_approval(
    pull_request: PullRequest,
    carried: Sequence[Review],
) -> str:
    """Whose earlier approvals still hold, and for which head commit."""
    approvals = [
        f"{review.author} at {review.commit}"
        for review in carried
    ]
    approved_at = ", ".join(approvals)
    return (
        f"Approved by {approved_at}; its own changes are identical at "
        f"{pull_request.head}."
    )


def judge(
    pull_request: PullRequest,
    maintainers: Sequence[str],
    diff_of: DiffReader,
) -> Verdict:
    """Decide whether the pull request may merge.

    Comparing diffs costs API calls, so it is the last resort: only when no
    cheaper reason lets the pull request pass.
    """
    governed = [path for path in pull_request.files if is_governed(path)]
    reasons = reasons_to_pass(pull_request, maintainers, governed)
    if reasons:
        return Verdict(has_passed=True, reason=reasons[0])
    else:
        return judge_by_earlier_approvals(
            pull_request,
            maintainers,
            governed,
            diff_of,
        )


def judge_by_earlier_approvals(
    pull_request: PullRequest,
    maintainers: Sequence[str],
    governed: Sequence[str],
    diff_of: DiffReader,
) -> Verdict:
    """Pass on an earlier approval whose changes match; otherwise fail."""
    carried = carried_approvals(pull_request, maintainers, diff_of)
    if carried:
        reason = describe_carried_approval(pull_request, carried)
        return Verdict(has_passed=True, reason=reason)
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
    summary = run(
        ["api", base, "--jq", "[.user.login, .head.sha, .base.ref] | @tsv"],
    )
    author, head, base_branch = summary.strip().split(FIELD_SEPARATOR)
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
        base=base_branch,
        files=files.split(),
        reviews=review_list,
    )


def to_changed_file(line: str) -> ChangedFile:
    """A changed file from one tab-separated line of `gh api` output."""
    path, status, previous_path, blob = line.split(FIELD_SEPARATOR)
    return ChangedFile(
        path=path,
        status=status,
        previous_path=previous_path,
        blob=blob,
    )


def compare_reader(
    repository: str,
    base_branch: str,
    run: CommandRunner,
) -> DiffReader:
    """Reads a commit's own changes: `git diff base...commit`, via GitHub.

    A failing call raises, so the check fails closed - for instance when an
    approved commit no longer exists after a force-push.
    """

    def diff_of(commit: str) -> Sequence[ChangedFile]:
        endpoint = f"repos/{repository}/compare/{base_branch}...{commit}"
        compared = run(["api", endpoint, "--jq", COMPARED_FILE_FIELDS])
        return [to_changed_file(line) for line in compared.splitlines()]

    return diff_of


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
    diff_of = compare_reader(repository, pull_request.base, run)
    verdict = judge(pull_request, maintainers, diff_of)
    print(verdict.reason)
    return exit_code_for(verdict.has_passed)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
