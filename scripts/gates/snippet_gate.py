"""The documentation snippet gate: docs must show the code the tests run.

Code in the docs is copied from tests by `dotnet mdsnippets`. If running it
would change a document, that document has drifted from the tested code.

The gate compares each document's content before and after the run rather
than asking git, so a developer's unrelated uncommitted edits cannot fail
it. It then puts every changed document back exactly as it was: a gate
reports, it never edits the working tree.
"""

from pathlib import Path

from gates.model import WHOLE_FILE, Finding, GateResult, Target
from gates.repository_files import repository_files
from gates.tools import REPOSITORY_ROOT, combined_output, execute

STALE_SNIPPET = "stale-snippet"

type Snapshot = dict[Path, bytes]


def snapshot_documents() -> Snapshot:
    """The current bytes of every Markdown file in the repository."""
    documents = repository_files("*.md")
    return {
        document: (REPOSITORY_ROOT / document).read_bytes()
        for document in documents
    }


def is_changed(document: Path, before: Snapshot) -> bool:
    """True when a document's bytes differ from its snapshot."""
    current = (REPOSITORY_ROOT / document).read_bytes()
    return current != before[document]


def restore(document: Path, before: Snapshot) -> None:
    """Put a document back as it was in the snapshot."""
    (REPOSITORY_ROOT / document).write_bytes(before[document])


def describe_drift(drifted: list[Path]) -> str:
    """Which documents are stale, and how to bring them up to date."""
    if drifted:
        names = "\n".join(f"  {document}" for document in drifted)
        return (
            "These documents show code that no longer matches the tests;"
            " run `dotnet mdsnippets` and commit the result:\n"
            f"{names}\n"
        )
    else:
        return ""


def run_snippets(_target: Target) -> GateResult:
    """Fail if `dotnet mdsnippets` would change any document."""
    before = snapshot_documents()
    completed = execute(["dotnet", "mdsnippets"])
    drifted = [document for document in before if is_changed(document, before)]
    for document in drifted:
        restore(document, before)
    findings = [
        Finding("snippets", STALE_SNIPPET, document.name, WHOLE_FILE)
        for document in drifted
    ]
    output = combined_output(completed) + describe_drift(drifted)
    return GateResult("snippets", completed.returncode, findings, output)
