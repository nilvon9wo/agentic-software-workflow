"""Tests for the house-rule pylint checkers in scripts/lint/."""

from textwrap import dedent

import astroid  # pyright: ignore[reportMissingTypeStubs] - astroid ships no stubs
from pylint.lint import PyLinter
from pylint.testutils import CheckerTestCase, MessageTest

import house_rules
from nested_block import NestedBlockChecker
from nested_call import NestedCallChecker
from no_conditional_expression import NoConditionalExpressionChecker


class HouseRuleTestCase(CheckerTestCase):
    """Runs one checker over a source snippet and returns what it reported."""

    def lint(self, source: str) -> list[tuple[str, int | None]]:
        """The (symbol, line) of each message the checker raises."""
        module = astroid.parse(dedent(source))
        self.walk(module)
        messages: list[MessageTest] = self.linter.release_messages()
        return [(message.msg_id, message.line) for message in messages]


class TestNoConditionalExpressionChecker(HouseRuleTestCase):
    """E9001: conditional expressions are banned outright."""

    CHECKER_CLASS = NoConditionalExpressionChecker

    def test_visit_ifexp_when_a_conditional_expression_is_used_reports_it(
        self,
    ) -> None:
        # Arrange
        source = "value = 1 if flag else 2"
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("no-conditional-expression", 1)]

    def test_visit_ifexp_when_an_if_statement_is_used_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            if flag:
                value = 1
            else:
                value = 2
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []


class TestNestedCallChecker(HouseRuleTestCase):
    """E9002: one call inside one call is the ceiling."""

    CHECKER_CLASS = NestedCallChecker

    def test_visit_call_when_a_call_is_nested_in_one_call_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = "outer(inner(value))"
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_call_when_nested_in_two_calls_reports_the_innermost(
        self,
    ) -> None:
        # Arrange
        source = "outer(middle(inner(value)))"
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("too-deeply-nested-call", 1)]

    def test_visit_call_when_in_a_comprehension_in_a_call_reports_it(
        self,
    ) -> None:
        # Arrange
        source = "total = sum(weight(item) for item in items)"
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("too-deeply-nested-call", 1)]

    def test_visit_call_when_calls_are_chained_fluently_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = "result = builder.first(one).second(two).third(three)"
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []


class TestNestedBlockChecker(HouseRuleTestCase):
    """E9003: two levels of blocks, or three when one is a try."""

    CHECKER_CLASS = NestedBlockChecker

    def test_check_block_when_blocks_nest_two_deep_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            def run(items):
                for item in items:
                    if item:
                        use(item)
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_check_block_when_blocks_nest_three_deep_reports_the_innermost(
        self,
    ) -> None:
        # Arrange
        source = """
            def run(groups):
                for group in groups:
                    for item in group:
                        if item:
                            use(item)
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("too-deeply-nested-block", 5)]

    def test_check_block_when_the_third_layer_is_a_try_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            def run(items):
                for item in items:
                    if item:
                        try:
                            use(item)
                        except ValueError:
                            skip(item)
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_check_block_when_a_try_holds_two_more_blocks_reports_the_innermost(
        self,
    ) -> None:
        # Arrange
        source = """
            def run(groups):
                try:
                    for group in groups:
                        for item in group:
                            if item:
                                use(item)
                except ValueError:
                    pass
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("too-deeply-nested-block", 6)]

    def test_check_block_when_a_chain_of_elifs_is_used_counts_it_as_one_level(
        self,
    ) -> None:
        # Arrange
        source = """
            def run(item):
                for part in item:
                    if part == 1:
                        use(part)
                    elif part == 2:
                        use(part)
                    elif part == 3:
                        use(part)
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_check_block_when_a_block_is_inside_an_elif_counts_one_level(
        self,
    ) -> None:
        # Arrange
        source = """
            def run(item, parts):
                if item == 1:
                    use(item)
                elif item == 2:
                    for part in parts:
                        use(part)
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_check_block_when_an_if_is_nested_in_an_else_counts_it_as_a_level(
        self,
    ) -> None:
        # Arrange
        source = """
            def run(item):
                for part in item:
                    if part:
                        use(part)
                    else:
                        if other(part):
                            use(part)
                        skip(part)
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("too-deeply-nested-block", 7)]

    def test_check_block_when_a_nested_function_starts_counting_again(
        self,
    ) -> None:
        # Arrange
        source = """
            def outer(items):
                for item in items:
                    def inner(parts):
                        for part in parts:
                            if part:
                                use(part)
                    inner(item)
            """
        reported: list[tuple[str, int | None]]

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []


def test_register_when_called_registers_every_house_rule_checker() -> None:
    # Arrange
    linter = PyLinter()
    registered: set[str]

    # Act
    house_rules.register(linter)

    # Assert
    registered = {checker.name for checker in linter.get_checkers()}
    assert {
        "no-conditional-expression",
        "too-deeply-nested-call",
        "too-deeply-nested-block",
    } <= registered
