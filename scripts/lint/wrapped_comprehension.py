"""E9006: a wrapped comprehension gives each clause its own line.

Once a comprehension spans lines, its element and every `for` and `if`
clause start a line of their own:

    [
        describe(violation)
        for violation in violations
        if violation.is_blocking
    ]
"""

# pyright: reportMissingTypeStubs=false
# astroid ships no type stubs; strict mode still checks its inferred types.
from astroid import nodes
from pylint.checkers import BaseChecker

from source_text import end_line, start_line, text_before

FOR_KEYWORDS = frozenset({"for", "async for"})
IF_KEYWORD = "if"

type Comprehension = (
    nodes.ListComp | nodes.SetComp | nodes.DictComp | nodes.GeneratorExp
)


def element(node: Comprehension) -> nodes.NodeNG:
    """What the comprehension produces: its element, or a dict's key."""
    if isinstance(node, nodes.DictComp):
        return node.key
    else:
        return node.elt


def misplaced_conditions(generator: nodes.Comprehension) -> list[str]:
    """Each `if` of one clause that does not start its own line."""
    leading_texts = [text_before(condition) for condition in generator.ifs]
    return [text for text in leading_texts if text != IF_KEYWORD]


def is_for_alone(generator: nodes.Comprehension) -> bool:
    """True when the clause's `for` starts its own line."""
    return text_before(generator.target) in FOR_KEYWORDS


def is_clause_alone(generator: nodes.Comprehension) -> bool:
    """True when a `for` clause and each of its `if`s start lines."""
    has_misplaced_conditions = len(misplaced_conditions(generator)) > 0
    return is_for_alone(generator) and not has_misplaced_conditions


def is_one_clause_per_line(node: Comprehension) -> bool:
    """True when the element and every clause start their own lines."""
    misplaced_clauses = [
        generator
        for generator in node.generators
        if not is_clause_alone(generator)
    ]
    is_element_alone = text_before(element(node)) == ""
    return is_element_alone and not misplaced_clauses


class WrappedComprehensionChecker(BaseChecker):
    """Rejects a wrapped comprehension whose clauses share lines."""

    name = "wrapped-comprehension-layout"
    msgs = {  # noqa: RUF012 - pylint reads this class attribute by name
        "E9006": (
            (
                "Wrapped comprehension: put the element and each for/if "
                "clause on its own line"
            ),
            "wrapped-comprehension-layout",
            "A comprehension that spans lines reads one clause per line.",
        ),
    }

    def check_comprehension(self, node: Comprehension) -> None:
        """Report a multi-line comprehension that shares lines."""
        is_wrapped = end_line(node) > start_line(node)
        if is_wrapped and not is_one_clause_per_line(node):
            self.add_message("wrapped-comprehension-layout", node=node)

    visit_listcomp = check_comprehension
    visit_setcomp = check_comprehension
    visit_dictcomp = check_comprehension
    visit_generatorexp = check_comprehension
