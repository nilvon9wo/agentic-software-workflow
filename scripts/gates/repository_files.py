"""Finding the repository's own files, as git sees them."""

from pathlib import Path

from gates.tools import REPOSITORY_ROOT, combined_output, execute

CANARY_DIRECTORY = Path("tests/StyleCanary")
WORKFLOW_DIRECTORY = Path(".github/workflows")


class RepositoryUnlistableError(Exception):
    """git could not list the files.

    Raised rather than treated as "no files", which would let every gate
    that depends on the list pass without checking anything.
    """


def repository_files(*patterns: str) -> list[Path]:
    """Files matching any pattern that are tracked, or new and not ignored.

    New files count so that a document still being written is checked
    before it is committed. Ignored ones (build output, caches) never are.
    A tracked file deleted from the working tree is gone, so it is skipped.
    """
    command = [
        "git",
        "ls-files",
        "--cached",
        "--others",
        "--exclude-standard",
        "--deduplicate",
        "--",
        *patterns,
    ]
    completed = execute(command)
    if completed.returncode != 0:
        raise RepositoryUnlistableError(combined_output(completed))
    listed = [Path(line) for line in completed.stdout.splitlines()]
    present = [path for path in listed if (REPOSITORY_ROOT / path).is_file()]
    return sorted(present)


def documents() -> list[Path]:
    """The repository's Markdown, except the deliberately broken canary."""
    markdown = repository_files("*.md")
    return [path for path in markdown if CANARY_DIRECTORY not in path.parents]


def workflows() -> list[Path]:
    """The repository's GitHub Actions workflows."""
    return repository_files(
        f"{WORKFLOW_DIRECTORY}/*.yml",
        f"{WORKFLOW_DIRECTORY}/*.yaml",
    )
