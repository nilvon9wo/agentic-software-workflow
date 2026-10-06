"""Lexical parenthesis matching for C# source.

String and comment contents are blanked out first (keeping line numbers),
then each `(` is paired with the `)` that closes it. This is lexical, not
syntactic: good enough for a layout check, not for anything that needs to
know what the code means.
"""

import re
from dataclasses import dataclass

OPENING_PAREN = "("
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


def blank_match(match: re.Match[str]) -> str:
    """Spaces in place of a match's characters, keeping its newlines."""
    matched_text = match.group()
    return NOT_NEWLINE.sub(" ", matched_text)


def blank_strings_and_comments(text: str) -> str:
    """Blank out literal and comment contents, keeping line numbers."""
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
    return [
        paren
        for number, line in numbered_lines
        for paren in parens_in_line(number, line)
    ]


def match_parens(code: str) -> list[ParenPair]:
    """Every matched pair of parentheses, in the order they close.

    `code` must already have its strings and comments blanked out. An
    unmatched `)` is ignored.
    """
    pairs: list[ParenPair] = []
    open_parens: list[Paren] = []
    for paren in find_parens(code):
        if paren.is_opening:
            open_parens.append(paren)
        elif open_parens:
            opening = open_parens.pop()
            pairs.append(ParenPair(opening, paren))
    return pairs
