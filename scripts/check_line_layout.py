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

MAX_LINE_LENGTH = 120
LINE_LENGTH_RULE = "line-length"
WRAP_RPAR_RULE = "wrap-rpar"
DEFAULT_ROOTS = ("src", "tests")
EXCLUDED_DIRECTORIES = frozenset({"bin", "obj", "StyleCanary"})
CONTROL_KEYWORDS = frozenset({"if", "while", "for", "foreach", "switch", "using", "lock", "catch", "when", "fixed"})
IDENTIFIER_BEFORE_PAREN = re.compile(r"([A-Za-z_]\w*)\s*(<[^()]*>)?\s*$")
STRING_OR_COMMENT = re.compile(
    r'//[^\n]*|/\*.*?\*/|""".*?"""|@"(?:[^"]|"")*"|"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])*\'',
    re.DOTALL,
)
NOT_NEWLINE = re.compile(r"[^\n]")


@dataclass(frozen=True)
class OpenParen:
    """A `(` awaiting its `)`: where it opened, and whether the layout rule governs it."""

    line_number: int
    is_call_or_declaration: bool


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


def blank_strings_and_comments(text: str) -> str:
    """Replace literal and comment contents with spaces, keeping newlines so line numbers survive."""
    return STRING_OR_COMMENT.sub(lambda match: NOT_NEWLINE.sub(" ", match.group()), text)


def is_call_or_declaration(text_before_paren: str) -> bool:
    """True when the `(` follows a name (optionally generic) that is not a control keyword."""
    match = IDENTIFIER_BEFORE_PAREN.search(text_before_paren)
    return match is not None and match.group(1) not in CONTROL_KEYWORDS


def is_dangling_close(opened: OpenParen, line_number: int, text_before_close: str) -> bool:
    """True when a multi-line call/declaration closes after other content on its last line."""
    is_multi_line = opened.line_number != line_number
    is_first_on_line = text_before_close.strip() == ""
    return opened.is_call_or_declaration and is_multi_line and not is_first_on_line


def find_dangling_close_parens(code: str) -> list[int]:
    """Line numbers on which a wrapped call or declaration closes after other content."""
    violations: list[int] = []
    open_parens: list[OpenParen] = []
    for line_number, line in enumerate(code.split("\n"), start=1):
        for column, character in enumerate(line):
            if character == "(":
                open_parens.append(OpenParen(line_number, is_call_or_declaration(line[:column])))
            elif character == ")" and open_parens and is_dangling_close(open_parens.pop(), line_number, line[:column]):
                violations.append(line_number)
    return violations


def check_file(path: Path) -> list[LayoutViolation]:
    """Every layout violation in one C# file."""
    text = path.read_text(encoding="utf-8-sig")
    long_lines = [
        LayoutViolation(path, number, LINE_LENGTH_RULE, f"{len(line)} characters (max {MAX_LINE_LENGTH})")
        for number, line in enumerate(text.splitlines(), start=1)
        if len(line) > MAX_LINE_LENGTH
    ]
    dangling_parens = [
        LayoutViolation(path, number, WRAP_RPAR_RULE, "wrapped call/declaration must close with ')' on its own line")
        for number in find_dangling_close_parens(blank_strings_and_comments(text))
    ]
    return long_lines + dangling_parens


def is_checked(path: Path) -> bool:
    """True for C# sources outside build output and the deliberately-broken canary."""
    return not EXCLUDED_DIRECTORIES.intersection(path.parts)


def expand(root: Path) -> list[Path]:
    """A file stands for itself (even the canary); a directory for its checked C# files."""
    return [root] if root.is_file() else sorted(path for path in root.rglob("*.cs") if is_checked(path))


def check_paths(roots: list[Path]) -> list[LayoutViolation]:
    """Every layout violation under the given files and directories."""
    return [violation for root in roots for path in expand(root) for violation in check_file(path)]


def main(arguments: list[str]) -> int:
    """Check the given files/directories (default: src and tests); exit 1 on any violation."""
    roots = [Path(argument) for argument in arguments or DEFAULT_ROOTS]
    violations = check_paths(roots)
    print("\n".join(violation.describe() for violation in violations) or "line layout: OK")
    return 1 if violations else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
