"""E9001: no conditional expressions (`a if condition else b`).

Python's conditional expression puts the condition in the middle and hides
a branch inside an expression. An if/else statement shows both branches.
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; strict mode still checks its inferred types.
from astroid import nodes
from pylint.checkers import BaseChecker


class NoConditionalExpressionChecker(BaseChecker):
    """Rejects every conditional expression."""

    name = "no-conditional-expression"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9001": (
            "Conditional expressions are not allowed; use if/else",
            "no-conditional-expression",
            "An if/else statement makes both branches visible.",
        ),
    }

    def visit_ifexp(self, node: nodes.IfExp) -> None:
        """Report the conditional expression."""
        self.add_message("no-conditional-expression", node=node)
