"""E9004: a boolean expression joins at most two booleans.

`a and b` is fine. `a and b and c`, or `a and (b or c)`, names its parts
first - house rule 7, "name the results of complex expressions". Keeping
chains this short also means the formatter never has a long chain to fold
back onto one line.

`not (...)` continues the expression it negates; a boolean expression
inside a call's arguments is a separate expression and counts on its own.
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; strict mode still checks its inferred types.
from typing import TypeGuard

from astroid import nodes
from pylint.checkers import BaseChecker

MAXIMUM_OPERATORS = 1
NOT_OPERATOR = "not"


def is_negation(node: nodes.NodeNG | None) -> TypeGuard[nodes.UnaryOp]:
    """True for `not <operand>`."""
    is_unary = isinstance(node, nodes.UnaryOp)
    return is_unary and node.op == NOT_OPERATOR


def operator_count(node: nodes.NodeNG) -> int:
    """The `and`/`or` operators in a boolean expression, nested ones too."""
    if isinstance(node, nodes.BoolOp):
        own_operators = len(node.values) - 1
        nested_operators = [operator_count(value) for value in node.values]
        return own_operators + sum(nested_operators)
    elif is_negation(node):
        return operator_count(node.operand)
    else:
        return 0


def is_outermost(node: nodes.BoolOp) -> bool:
    """True unless `node` is part of a larger boolean expression."""
    ancestor = node.parent
    while is_negation(ancestor):
        ancestor = ancestor.parent
    return not isinstance(ancestor, nodes.BoolOp)


class BooleanChainChecker(BaseChecker):
    """Rejects a boolean expression with more than one `and`/`or`."""

    name = "too-long-boolean-chain"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9004": (
            "Boolean chain has %d and/or operators (max %d); name its parts",
            "too-long-boolean-chain",
            "Name the results of complex expressions (house rule 7).",
        ),
    }

    def visit_boolop(self, node: nodes.BoolOp) -> None:
        """Report the whole expression once, at its outermost operator."""
        if is_outermost(node):
            count = operator_count(node)
            if count > MAXIMUM_OPERATORS:
                counts = (count, MAXIMUM_OPERATORS)
                self.add_message(
                    "too-long-boolean-chain",
                    node=node,
                    args=counts,
                )
