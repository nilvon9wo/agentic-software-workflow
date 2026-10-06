"""Pylint checkers for the house rules no off-the-shelf Python linter enforces.

Each checker is the Python counterpart of a rule in
docs/contribute/coding-standards.md:

- E9001 no-conditional-expression: `a if condition else b` hides a branch
  inside an expression; use an if/else statement.
- E9002 too-deeply-nested-call: never nest expressions. One call inside one
  call is the ceiling; a comprehension counts as a level.
- E9003 too-deeply-nested-block: blocks nest at most two deep, except that a
  try/except may be the third layer.

Load with `load-plugins = ["house_rules"]` (see pyproject.toml).
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; its types are inferred from source, which strict mode still checks.
from astroid import nodes
from pylint.checkers import BaseChecker
from pylint.lint import PyLinter

MAXIMUM_CALL_DEPTH = 2
MAXIMUM_BLOCK_DEPTH = 2
MAXIMUM_BLOCK_DEPTH_WITH_TRY = 3
COMPREHENSIONS = (nodes.ListComp, nodes.SetComp, nodes.DictComp, nodes.GeneratorExp)
BLOCKS = (nodes.If, nodes.For, nodes.While, nodes.With, nodes.Match, nodes.Try)
SCOPES = (nodes.FunctionDef, nodes.ClassDef, nodes.Module, nodes.Lambda)


class NoConditionalExpressionChecker(BaseChecker):
    """Rejects every conditional expression (`a if condition else b`)."""

    name = "no-conditional-expression"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9001": (
            "Conditional expressions are not allowed; use an if/else statement",
            "no-conditional-expression",
            "An if/else statement makes both branches visible; see coding-standards.md.",
        ),
    }

    def visit_ifexp(self, node: nodes.IfExp) -> None:
        """Report the conditional expression."""
        self.add_message("no-conditional-expression", node=node)


def is_nesting_level(child: nodes.NodeNG, parent: nodes.NodeNG) -> bool:
    """True when `child` sits in a call's arguments or anywhere in a comprehension.

    A call's *callee* (`a.b().c()`) is not nesting: a fluent chain reads left
    to right. Its arguments are.
    """
    is_call_argument = isinstance(parent, nodes.Call) and child is not parent.func
    is_in_comprehension = isinstance(parent, COMPREHENSIONS)
    return is_call_argument or is_in_comprehension


def call_depth(node: nodes.NodeNG) -> int:
    """How many calls or comprehensions enclose `node` within its statement."""
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
    """Rejects a call nested inside a call inside a call (or comprehension)."""

    name = "too-deeply-nested-call"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9002": (
            "Call nested %d levels deep (maximum %d); name the inner result with a variable",
            "too-deeply-nested-call",
            "Never nest expressions: one call inside one call is the ceiling.",
        ),
    }

    def visit_call(self, node: nodes.Call) -> None:
        """Report the call when too many calls or comprehensions enclose it."""
        depth = call_depth(node)
        if depth >= MAXIMUM_CALL_DEPTH:
            self.add_message("too-deeply-nested-call", node=node, args=(depth + 1, MAXIMUM_CALL_DEPTH))


def is_elif(node: nodes.NodeNG) -> bool:
    """True for the `if` astroid uses to represent an `elif`: same level, not deeper."""
    parent = node.parent
    return isinstance(node, nodes.If) and isinstance(parent, nodes.If) and parent.orelse == [node]


def enclosing_blocks(node: nodes.NodeNG) -> list[nodes.NodeNG]:
    """The blocks that enclose `node`, innermost first, up to its function or class."""
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
    """True when `node` breaks the two-deep rule, allowing try/except as one extra layer."""
    blocks = [node, *enclosing_blocks(node)]
    blocks_without_try = [block for block in blocks if not is_try(block)]
    depth = len(blocks)
    depth_without_try = len(blocks_without_try)
    is_beyond_try_allowance = depth > MAXIMUM_BLOCK_DEPTH_WITH_TRY
    return depth_without_try > MAXIMUM_BLOCK_DEPTH or is_beyond_try_allowance


class NestedBlockChecker(BaseChecker):
    """Rejects blocks nested more than two deep (three when one of them is a try)."""

    name = "too-deeply-nested-block"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9003": (
            "Block nested too deeply; at most two levels, or three when one is a try/except",
            "too-deeply-nested-block",
            "Extract the inner block into a named function.",
        ),
    }

    def check_block(self, node: nodes.NodeNG) -> None:
        """Report the block if it is nested too deeply; an elif is not a new level."""
        if not is_elif(node) and is_too_deep(node):
            self.add_message("too-deeply-nested-block", node=node)

    visit_if = check_block
    visit_for = check_block
    visit_while = check_block
    visit_with = check_block
    visit_match = check_block
    visit_try = check_block


def register(linter: PyLinter) -> None:
    """Pylint's plugin entry point."""
    linter.register_checker(NoConditionalExpressionChecker(linter))
    linter.register_checker(NestedCallChecker(linter))
    linter.register_checker(NestedBlockChecker(linter))
