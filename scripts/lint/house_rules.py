"""Pylint plugin: the house rules no off-the-shelf Python linter enforces.

Each checker lives in its own module and mirrors a rule in
docs/contribute/coding-standards.md. Loaded by pyproject.toml's
`load-plugins = ["house_rules", ...]`.
"""

from pylint.lint import PyLinter

from boolean_chain import BooleanChainChecker
from nested_block import NestedBlockChecker
from nested_call import NestedCallChecker
from no_conditional_expression import NoConditionalExpressionChecker


def register(linter: PyLinter) -> None:
    """Pylint's plugin entry point."""
    linter.register_checker(NoConditionalExpressionChecker(linter))
    linter.register_checker(NestedCallChecker(linter))
    linter.register_checker(NestedBlockChecker(linter))
    linter.register_checker(BooleanChainChecker(linter))
