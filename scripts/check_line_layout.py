#!/usr/bin/env python3
"""Enforce the two .editorconfig layout rules that no Roslyn analyzer checks.

1. max_line_length = 120 -- Roslyn reads the setting but never reports on it.
2. csharp_wrap_before_invocation_rpar / _declaration_rpar -- a call or
   declaration whose argument list spans lines closes with `)` on its own line.
   Only ReSharper/Rider honor these, and `jb cleanupcode` does not fix them.

This is a deliberately small, lexical stop-gap: string and comment contents are
blanked out before parentheses are matched, and parentheses opened by a control
keyword (`if (`, `foreach (`) are ignored. It is slated to be replaced by a
Roslyn analyzer, which can tell an invocation from any other parenthesis
exactly. Until then, scripts/verify_gates.py proves this check still fires.

Usage: check_line_layout.py [path ...]   (default: src tests)
Exit code 1 and one `path:line: rule: message` per violation when any rule fails.
"""

import re
import sys
from dataclasses import dataclass
from pathlib import Path

from exit_codes import exit_code_for

MAX_LINE_LENGTH = 120
LINE_LENGTH_RULE = "line-length"
WRAP_RPAR_RULE = "wrap-rpar"
WRAP_RPAR_MESSAGE = "wrapped call/declaration must close with ')' on its own line"
OPENING_PAREN = "("
DEFAULT_ROOTS = ("src", "tests")
EXCLUDED_DIRECTORIES = frozenset({"bin", "obj", "StyleCanary"})
CONTROL_KEYWORDS = frozenset(
    {"if", "while", "for", "foreach", "switch", "using", "lock", "catch", "when", "fixed"},
)
IDENTIFIER_BEFORE_PAREN = re.compile(r"([A-Za-z_]\w*)\s*(<[^()]*>)?\s*$")
PAREN = re.compile(r"[()]")
NOT_NEWLINE = re.compile(r"[^\n]")
STRING_OR_COMMENT = re.compile(
    r"//[^\n]*"  # line comment
    r"|/\*.*?\*/"  # block comment
    r'|""".*?"""'  # raw string literal
    r'|@"(?:[^"]|"")*"'  # verbatim string
    r'|"(?:\\.|[^"\\\n])*"'  # regular string
    r"|'(?:\\.|[^'\\\n])*'",  # character literal
    re.DOTALL,
)


@dataclass(frozen=True)
class Paren:
    """One parenthesis: where it is, and what precedes it on its line."""

    line_number: int
    text_before: str
    is_opening: bool


@dataclass(frozen=True)
class ParenPair:
    """A `(` and the `)` that closes it."""

    opening: Paren
    closing: Paren


@dataclass(frozen=True)
class LayoutViolation:
    """One broken layout rule at one line of one file."""

    path: Path
    line_number: int
    rule: str
    message: str

    def describe(self) -> str:
        """The `path:line: rule: message` form editors and CI logs link to."""
        return f"{self.path}:{self.line_number}: {self.rule}: {self.message}"


def blank_match(match: re.Match[str]) -> str:
    """Spaces in place of a match's characters, keeping its newlines."""
    matched_text = match.group()
    return NOT_NEWLINE.sub(" ", matched_text)


def blank_strings_and_comments(text: str) -> str:
    """Blank out literal and comment contents, keeping line numbers intact."""
    return STRING_OR_COMMENT.sub(blank_match, text)


def paren_at(line_number: int, line: str, match: re.Match[str]) -> Paren:
    """The parenthesis a regex match found on a line."""
    column = match.start()
    text_before = line[:column]
    is_opening = match.group() == OPENING_PAREN
    return Paren(line_number, text_before, is_opening)


def parens_in_line(line_number: int, line: str) -> list[Paren]:
    """Every parenthesis on one line, left to right."""
    matches = PAREN.finditer(line)
    return [paren_at(line_number, line, match) for match in matches]


def find_parens(code: str) -> list[Paren]:
    """Every parenthesis in the code, in reading order."""
    lines = code.split("\n")
    numbered_lines = enumerate(lines, start=1)
    return [paren for number, line in numbered_lines for paren in parens_in_line(number, line)]


def match_parens(code: str) -> list[ParenPair]:
    """Every matched pair of parentheses, in the order they close."""
    pairs: list[ParenPair] = []
    open_parens: list[Paren] = []
    for paren in find_parens(code):
        if paren.is_opening:
            open_parens.append(paren)
        elif open_parens:
            opening = open_parens.pop()
            pairs.append(ParenPair(opening, paren))
    return pairs


def is_call_or_declaration(text_before_paren: str) -> bool:
    """True when the `(` follows a name (optionally generic) that is not a control keyword."""
    match = IDENTIFIER_BEFORE_PAREN.search(text_before_paren)
    if match is None:
        return False
    else:
        name = match.group(1)
        return name not in CONTROL_KEYWORDS


def is_dangling(pair: ParenPair) -> bool:
    """True when a multi-line call/declaration closes after other content on its last line."""
    is_governed = is_call_or_declaration(pair.opening.text_before)
    is_multi_line = pair.opening.line_number != pair.closing.line_number
    is_first_on_line = pair.closing.text_before.strip() == ""
    return is_governed and is_multi_line and not is_first_on_line


def long_line_violation(path: Path, line_number: int, line: str) -> LayoutViolation:
    """The violation for one over-long line."""
    message = f"{len(line)} characters (max {MAX_LINE_LENGTH})"
    return LayoutViolation(path, line_number, LINE_LENGTH_RULE, message)


def find_long_lines(path: Path, text: str) -> list[LayoutViolation]:
    """Every line longer than the maximum."""
    lines = text.splitlines()
    numbered_lines = enumerate(lines, start=1)
    return [long_line_violation(path, number, line) for number, line in numbered_lines if len(line) > MAX_LINE_LENGTH]


def find_dangling_parens(path: Path, text: str) -> list[LayoutViolation]:
    """Every wrapped call or declaration whose `)` is not on a line of its own."""
    code = blank_strings_and_comments(text)
    pairs = match_parens(code)
    dangling_pairs = [pair for pair in pairs if is_dangling(pair)]
    return [
        LayoutViolation(path, pair.closing.line_number, WRAP_RPAR_RULE, WRAP_RPAR_MESSAGE) for pair in dangling_pairs
    ]


def check_file(path: Path) -> list[LayoutViolation]:
    """Every layout violation in one C# file."""
    text = path.read_text(encoding="utf-8-sig")
    long_lines = find_long_lines(path, text)
    dangling_parens = find_dangling_parens(path, text)
    return long_lines + dangling_parens


def is_checked(path: Path) -> bool:
    """True for C# sources outside build output and the deliberately broken canary."""
    excluded_parts = EXCLUDED_DIRECTORIES.intersection(path.parts)
    return not excluded_parts


def expand(root: Path) -> list[Path]:
    """A file stands for itself (even the canary); a directory for its checked C# files."""
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
    """Check the given files/directories (default: src and tests); exit 1 on any violation."""
    roots = to_roots(arguments)
    violations = check_paths(roots)
    report(violations)
    has_no_violations = not violations
    return exit_code_for(has_no_violations)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
