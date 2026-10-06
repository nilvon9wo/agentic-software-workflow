"""E9003: blocks nest at most two deep; a try/except may be a third layer.

An `elif` is not a new level: it continues its `if`. A nested function or
class starts counting again.
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; strict mode still checks its inferred types.
from astroid import nodes
from pylint.checkers import BaseChecker

MAXIMUM_BLOCK_DEPTH = 2
MAXIMUM_BLOCK_DEPTH_WITH_TRY = 3
BLOCKS = (
    nodes.If,
    nodes.For,
    nodes.While,
    nodes.With,
    nodes.Match,
    nodes.Try,
)
SCOPES = (nodes.FunctionDef, nodes.ClassDef, nodes.Module, nodes.Lambda)


def is_elif(node: nodes.NodeNG) -> bool:
    """True for the `if` astroid uses to represent an `elif`."""
    parent = node.parent
    is_if = isinstance(node, nodes.If)
    is_inside_if = isinstance(parent, nodes.If)
    return is_if and is_inside_if and parent.orelse == [node]


def enclosing_blocks(node: nodes.NodeNG) -> list[nodes.NodeNG]:
    """The blocks enclosing `node`, innermost first, within its scope."""
    blocks: list[nodes.NodeNG] = []
    child = node
    parent = node.parent
    while parent is not None and not isinstance(parent, SCOPES):
        if isinstance(parent, BLOCKS) and not is_elif(child):
            blocks.append(parent)
        child = parent
        parent = parent.parent
    return blocks


def is_try(block: nodes.NodeNG) -> bool:
    """True for a try/except (or try/finally) block."""
    return isinstance(block, nodes.Try)


def is_too_deep(node: nodes.NodeNG) -> bool:
    """True when `node` breaks the rule, allowing one try/except layer."""
    blocks = [node, *enclosing_blocks(node)]
    blocks_without_try = [block for block in blocks if not is_try(block)]
    depth = len(blocks)
    depth_without_try = len(blocks_without_try)
    is_beyond_try_allowance = depth > MAXIMUM_BLOCK_DEPTH_WITH_TRY
    is_beyond_plain_limit = depth_without_try > MAXIMUM_BLOCK_DEPTH
    return is_beyond_plain_limit or is_beyond_try_allowance


class NestedBlockChecker(BaseChecker):
    """Rejects blocks nested more than two deep (three with a try)."""

    name = "too-deeply-nested-block"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9003": (
            "Block nested too deeply (2 levels, or 3 when one is a try)",
            "too-deeply-nested-block",
            "Extract the inner block into a named function.",
        ),
    }

    def check_block(self, node: nodes.NodeNG) -> None:
        """Report the block if it is nested too deeply."""
        if not is_elif(node) and is_too_deep(node):
            self.add_message("too-deeply-nested-block", node=node)

    visit_if = check_block
    visit_for = check_block
    visit_while = check_block
    visit_with = check_block
    visit_match = check_block
    visit_try = check_block
