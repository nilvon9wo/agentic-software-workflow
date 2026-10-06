"""E9002: never nest expressions; one call inside one call is the ceiling.

A call in another call's arguments is one level; a comprehension is one
level too. A fluent chain (`a.b().c()`) is not nesting: it reads left to
right.
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; strict mode still checks its inferred types.
from astroid import nodes
from pylint.checkers import BaseChecker

MAXIMUM_CALL_DEPTH = 2
COMPREHENSIONS = (
    nodes.ListComp,
    nodes.SetComp,
    nodes.DictComp,
    nodes.GeneratorExp,
)


def is_nesting_level(child: nodes.NodeNG, parent: nodes.NodeNG) -> bool:
    """True when `child` is a call's argument or part of a comprehension."""
    is_call_argument = (
        isinstance(parent, nodes.Call) and child is not parent.func
    )
    is_in_comprehension = isinstance(parent, COMPREHENSIONS)
    return is_call_argument or is_in_comprehension


def call_depth(node: nodes.NodeNG) -> int:
    """How many calls or comprehensions enclose `node` in its statement."""
    depth = 0
    child = node
    parent = node.parent
    while parent is not None and not parent.is_statement:
        if is_nesting_level(child, parent):
            depth += 1
        child = parent
        parent = parent.parent
    return depth


class NestedCallChecker(BaseChecker):
    """Rejects a call nested inside a call inside a call."""

    name = "too-deeply-nested-call"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9002": (
            "Call nested %d levels deep (maximum %d); name the inner result",
            "too-deeply-nested-call",
            "Never nest expressions: one call inside one call is the ceiling.",
        ),
    }

    def visit_call(self, node: nodes.Call) -> None:
        """Report the call when too many calls or comprehensions enclose it."""
        depth = call_depth(node)
        if depth >= MAXIMUM_CALL_DEPTH:
            levels = (depth + 1, MAXIMUM_CALL_DEPTH)
            self.add_message("too-deeply-nested-call", node=node, args=levels)
