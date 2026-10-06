"""E9005: a wrapped call, signature, or collection has one item per line.

Python's counterpart of the C# wrapping rule. Once arguments, parameters,
or elements spill past the opening line, every item starts a line of its
own and the closing bracket sits alone on its line:

    run_gate(
        "ruff",
        command,
        target=target,
    )

Parameters are checked up to and including keyword-only ones; `*args`
and `**kwargs` are not checked.
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; strict mode still checks its inferred types.
from collections.abc import Iterable, Sequence

from astroid import nodes
from pylint.checkers import BaseChecker

from source_text import (
    closes_on_own_line,
    end_line,
    line_text,
    start_line,
    starts_line,
)

CLOSING_PAREN = ")"

type Items = Sequence[nodes.NodeNG]


def as_nodes(values: Iterable[object]) -> list[nodes.NodeNG]:
    """The syntax nodes among astroid's loosely typed child lists."""
    return [value for value in values if isinstance(value, nodes.NodeNG)]


def are_items_alone(items: Items) -> bool:
    """True when every item starts its own line."""
    misplaced_items = [item for item in items if not starts_line(item)]
    return not misplaced_items


def dict_items(node: nodes.Dict) -> list[nodes.NodeNG]:
    """The node starting each entry (a `**spread` entry starts with `**`)."""
    keys = [key for key, _value in node.items]
    return as_nodes(keys)


def parameters(arguments: nodes.Arguments) -> list[nodes.NodeNG]:
    """The named parameters, in order."""
    named = [
        *arguments.posonlyargs,
        *(arguments.args or []),
        *arguments.kwonlyargs,
    ]
    return as_nodes(named)


def parameter_parts(arguments: nodes.Arguments) -> list[nodes.NodeNG]:
    """Every node a parameter list contains: names, annotations, defaults."""
    candidates = [
        *parameters(arguments),
        *arguments.annotations,
        *arguments.kwonlyargs_annotations,
        *arguments.posonlyargs_annotations,
        *(arguments.defaults or []),
        *(arguments.kw_defaults or []),
    ]
    return as_nodes(candidates)


def last_parameter_line(arguments: nodes.Arguments) -> int:
    """The last line any part of the parameter list reaches."""
    part_ends = [end_line(part) for part in parameter_parts(arguments)]
    return max(part_ends)


def is_closed_on_next_line(node: nodes.FunctionDef, last_line: int) -> bool:
    """True when the line after the parameters starts with `)`."""
    next_line = line_text(node, last_line + 1)
    stripped_line = next_line.strip()
    return stripped_line.startswith(CLOSING_PAREN)


def def_line(node: nodes.FunctionDef) -> int:
    """The line of the `def` keyword itself, below any decorators."""
    position = node.position
    if position is None:
        return start_line(node)
    else:
        return position.lineno


def is_signature_wrapped(node: nodes.FunctionDef) -> bool:
    """True when the parameters continue past the `def` line."""
    return last_parameter_line(node.args) > def_line(node)


def is_signature_laid_out(
    node: nodes.FunctionDef,
    named_parameters: Items,
) -> bool:
    """True when each parameter starts a line and `)` follows alone."""
    last_line = last_parameter_line(node.args)
    is_closed = is_closed_on_next_line(node, last_line)
    return are_items_alone(named_parameters) and is_closed


class WrappedItemsChecker(BaseChecker):
    """Rejects a wrapped call, signature, or collection sharing lines."""

    name = "wrapped-items-layout"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9005": (
            (
                "Wrapped items must each start a line, with the closing "
                "bracket alone on its line"
            ),
            "wrapped-items-layout",
            "The Python counterpart of the C# wrapping rule.",
        ),
    }

    def report_unless(self, node: nodes.NodeNG, *, is_laid_out: bool) -> None:
        """Report `node` unless its layout is correct."""
        if not is_laid_out:
            self.add_message("wrapped-items-layout", node=node)

    def check_bracketed(self, node: nodes.NodeNG, items: Items) -> None:
        """Check a bracketed list of items that spans several lines."""
        is_laid_out = are_items_alone(items) and closes_on_own_line(node)
        self.report_unless(node, is_laid_out=is_laid_out)

    def visit_call(self, node: nodes.Call) -> None:
        """A call is wrapped when its arguments leave the callee's line."""
        if end_line(node) > end_line(node.func):
            items = [*node.args, *node.keywords]
            self.check_bracketed(node, items)

    def check_collection(self, node: nodes.NodeNG, items: Items) -> None:
        """A collection is wrapped when it spans more than one line."""
        if end_line(node) > start_line(node):
            self.check_bracketed(node, items)

    def visit_list(self, node: nodes.List) -> None:
        """Check a list display."""
        self.check_collection(node, as_nodes(node.elts))

    def visit_set(self, node: nodes.Set) -> None:
        """Check a set display."""
        self.check_collection(node, as_nodes(node.elts))

    def visit_tuple(self, node: nodes.Tuple) -> None:
        """Check a tuple display."""
        self.check_collection(node, as_nodes(node.elts))

    def visit_dict(self, node: nodes.Dict) -> None:
        """Check a dict display."""
        self.check_collection(node, dict_items(node))

    def visit_functiondef(self, node: nodes.FunctionDef) -> None:
        """Check a signature whose parameters leave the def line."""
        named_parameters = parameters(node.args)
        if named_parameters and is_signature_wrapped(node):
            is_laid_out = is_signature_laid_out(node, named_parameters)
            self.report_unless(node, is_laid_out=is_laid_out)

    visit_asyncfunctiondef = visit_functiondef
