"""E9004: a chain of three or more booleans puts one operand per line.

`return foo and bar` is fine on one line. A longer chain is laid out with
each operand on a line of its own, the operator leading:

    return (
        foo
        and bar
        and bat
    )

Breaking the line, not inventing names, is the fix: a name must say what
something means, never exist just to shorten a line.
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; strict mode still checks its inferred types.
from astroid import nodes
from pylint.checkers import BaseChecker

from source_text import starts_line, text_before

MAXIMUM_INLINE_OPERANDS = 2


def is_led_by(operand: nodes.NodeNG, operator: str) -> bool:
    """True when `operand` starts its line, preceded only by `operator`."""
    return text_before(operand) == operator


def is_one_operand_per_line(node: nodes.BoolOp) -> bool:
    """True when every operand has its own line, operators leading."""
    first_operand, *other_operands = node.values
    misplaced_operands = [
        operand
        for operand in other_operands
        if not is_led_by(operand, node.op)
    ]
    return starts_line(first_operand) and not misplaced_operands


class BooleanChainChecker(BaseChecker):
    """Rejects a long boolean chain that is not one operand per line."""

    name = "boolean-chain-layout"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9004": (
            (
                "Chain of %d booleans: put each operand on its own line, "
                "operator first"
            ),
            "boolean-chain-layout",
            "Two booleans may share a line; longer chains may not.",
        ),
    }

    def visit_boolop(self, node: nodes.BoolOp) -> None:
        """Report a chain longer than two that shares lines."""
        operand_count = len(node.values)
        is_long_chain = operand_count > MAXIMUM_INLINE_OPERANDS
        if is_long_chain and not is_one_operand_per_line(node):
            self.add_message(
                "boolean-chain-layout",
                node=node,
                args=(operand_count,),
            )
