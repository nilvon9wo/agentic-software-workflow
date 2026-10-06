#!/usr/bin/env python3
"""Enforce the two .editorconfig layout rules no Roslyn analyzer checks.

1. max_line_length = 120. Roslyn reads the setting but never reports on it.
2. csharp_wrap_before_invocation_rpar / _declaration_rpar: a call or
   declaration whose argument list spans lines closes with `)` on its own
   line. Only ReSharper/Rider honour these; `jb cleanupcode` does not fix
   them.

A deliberately small, lexical stop-gap (see csharp_parens.py), to be
replaced by a Roslyn analyzer (issue #3). Until then, verify_gates.py
proves this check still fires.

Usage: check_line_layout.py [path ...]   (default: src tests)
Exits 1, printing `path:line: rule: message` per violation, if any rule
fails.
"""

import re
import sys
from dataclasses import dataclass
from pathlib import Path

from csharp_parens import ParenPair, blank_strings_and_comments, match_parens
from exit_codes import exit_code_for

MAX_LINE_LENGTH = 120
LINE_LENGTH_RULE = "line-length"
WRAP_RPAR_RULE = "wrap-rpar"
WRAP_RPAR_MESSAGE = (
    "wrapped call/declaration must close with ')' on its own line"
)
DEFAULT_ROOTS = ("src", "tests")
EXCLUDED_DIRECTORIES = frozenset({"bin", "obj", "StyleCanary"})
CONTROL_KEYWORDS = frozenset(
    {
        "if",
        "while",
        "for",
        "foreach",
        "switch",
        "using",
        "lock",
        "catch",
        "when",
        "fixed",
    },
)
IDENTIFIER_BEFORE_PAREN = re.compile(r"([A-Za-z_]\w*)\s*(<[^()]*>)?\s*$")


@dataclass(frozen=True)
class LayoutViolation:
    """One broken layout rule at one line of one file."""

    path: Path
    line_number: int
    rule: str
    message: str

    def describe(self) -> str:
        """The `path:line: rule: message` form editors and CI logs link to."""
        location = f"{self.path}:{self.line_number}"
        return f"{location}: {self.rule}: {self.message}"


def is_call_or_declaration(text_before_paren: str) -> bool:
    """True when `(` follows a name that is not a control keyword."""
    match = IDENTIFIER_BEFORE_PAREN.search(text_before_paren)
    if match is None:
        return False
    else:
        name = match.group(1)
        return name not in CONTROL_KEYWORDS


def is_dangling(pair: ParenPair) -> bool:
    """True when a wrapped call/declaration closes after other content."""
    is_governed = is_call_or_declaration(pair.opening.text_before)
    is_multi_line = pair.opening.line_number != pair.closing.line_number
    is_first_on_line = pair.closing.text_before.strip() == ""
    return is_governed and is_multi_line and not is_first_on_line


def long_line_violation(
    path: Path,
    line_number: int,
    line: str,
) -> LayoutViolation:
    """The violation for one over-long line."""
    message = f"{len(line)} characters (max {MAX_LINE_LENGTH})"
    return LayoutViolation(path, line_number, LINE_LENGTH_RULE, message)


def dangling_paren_violation(path: Path, pair: ParenPair) -> LayoutViolation:
    """The violation for one wrapped call whose `)` is not on its own line."""
    line_number = pair.closing.line_number
    return LayoutViolation(path, line_number, WRAP_RPAR_RULE, WRAP_RPAR_MESSAGE)


def find_long_lines(path: Path, text: str) -> list[LayoutViolation]:
    """Every line longer than the maximum."""
    lines = text.splitlines()
    numbered_lines = enumerate(lines, start=1)
    return [
        long_line_violation(path, number, line)
        for number, line in numbered_lines
        if len(line) > MAX_LINE_LENGTH
    ]


def find_dangling_parens(path: Path, text: str) -> list[LayoutViolation]:
    """Every wrapped call or declaration whose `)` shares a line."""
    code = blank_strings_and_comments(text)
    pairs = match_parens(code)
    dangling_pairs = [pair for pair in pairs if is_dangling(pair)]
    return [dangling_paren_violation(path, pair) for pair in dangling_pairs]


def check_file(path: Path) -> list[LayoutViolation]:
    """Every layout violation in one C# file."""
    text = path.read_text(encoding="utf-8-sig")
    long_lines = find_long_lines(path, text)
    dangling_parens = find_dangling_parens(path, text)
    return long_lines + dangling_parens


def is_checked(path: Path) -> bool:
    """True for sources outside build output and the deliberate canary."""
    excluded_parts = EXCLUDED_DIRECTORIES.intersection(path.parts)
    return not excluded_parts


def expand(root: Path) -> list[Path]:
    """A file stands for itself; a directory for its checked C# files.

    A file named explicitly is checked even inside the canary: that is how
    verify_gates.py proves this check fires.
    """
    if root.is_file():
        return [root]
    else:
        candidates = root.rglob("*.cs")
        checked = [path for path in candidates if is_checked(path)]
        return sorted(checked)


def check_paths(roots: list[Path]) -> list[LayoutViolation]:
    """Every layout violation under the given files and directories."""
    files = [path for root in roots for path in expand(root)]
    return [violation for path in files for violation in check_file(path)]


def to_roots(arguments: list[str]) -> list[Path]:
    """The paths named on the command line, or the default roots."""
    if arguments:
        names = arguments
    else:
        names = list(DEFAULT_ROOTS)
    return [Path(name) for name in names]


def report(violations: list[LayoutViolation]) -> None:
    """Print every violation, or a one-line all-clear."""
    if violations:
        descriptions = [violation.describe() for violation in violations]
        print("\n".join(descriptions))
    else:
        print("line layout: OK")


def main(arguments: list[str]) -> int:
    """Check the given paths (default: src and tests); 1 on any violation."""
    roots = to_roots(arguments)
    violations = check_paths(roots)
    report(violations)
    has_no_violations = not violations
    return exit_code_for(has_no_violations)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
