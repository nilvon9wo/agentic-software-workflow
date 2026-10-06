"""Reading the source text around an astroid node.

Layout rules need what the syntax tree leaves out: which line a token
starts, and what precedes it there.
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; strict mode still checks its inferred types.
from functools import cache

from astroid import nodes

OPENING_BRACKETS = " ([{"


@cache
def module_lines(module: nodes.Module) -> list[str]:
    """A module's source, as lines; read once per module.

    A module built without source text (never the case for a linted file)
    has no lines.
    """
    stream = module.stream()
    if stream is None:
        return []
    else:
        with stream:
            source = stream.read()
        text = source.decode("utf-8")
        return text.splitlines()


def line_text(node: nodes.NodeNG, line_number: int) -> str:
    """One line (counting from 1) of the module containing `node`."""
    module = node.root()
    lines = module_lines(module)
    return lines[line_number - 1]


def start_line(node: nodes.NodeNG) -> int:
    """The line `node` starts on (0 if astroid knows no position)."""
    return node.lineno or 0


def end_line(node: nodes.NodeNG) -> int:
    """The line `node` ends on (0 if astroid knows no position)."""
    return node.end_lineno or 0


def text_before(node: nodes.NodeNG) -> str:
    """What precedes `node` on its first line, ignoring opening brackets.

    `    and (` before an operand reads as `and`; `    (` reads as nothing.
    """
    line = line_text(node, start_line(node))
    column = node.col_offset or 0
    preceding = line[:column]
    without_brackets = preceding.rstrip(OPENING_BRACKETS)
    return without_brackets.strip()


def starts_line(node: nodes.NodeNG) -> bool:
    """True when nothing but whitespace or brackets precedes `node`."""
    return text_before(node) == ""


def closes_on_own_line(node: nodes.NodeNG) -> bool:
    """True when the bracket that ends `node` begins its line."""
    line = line_text(node, end_line(node))
    closing_column = (node.end_col_offset or 0) - 1
    preceding = line[:closing_column]
    return preceding.strip() == ""
