"""Tests for the layout checkers: E9004, E9005, E9006, and source_text."""

import astroid  # pyright: ignore[reportMissingTypeStubs] - astroid ships no stubs
from checker_testing import HouseRuleTestCase

from boolean_chain import BooleanChainChecker
from source_text import module_lines
from wrapped_comprehension import WrappedComprehensionChecker
from wrapped_items import WrappedItemsChecker, def_line

type Reports = list[tuple[str, int | None]]

START_LINE = 7


class TestBooleanChainChecker(HouseRuleTestCase):
    """E9004: three or more booleans go one operand per line."""

    CHECKER_CLASS = BooleanChainChecker

    def test_visit_boolop_when_two_booleans_share_a_line_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = "is_ready = is_built and is_tested"
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_boolop_when_three_booleans_share_a_line_reports_it(
        self,
    ) -> None:
        # Arrange
        source = "is_ready = is_built and is_tested and is_reviewed"
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("boolean-chain-layout", 1)]

    def test_visit_boolop_when_one_operand_per_line_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            is_ready = (
                is_built
                and is_tested
                and not is_blocked
            )
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_boolop_when_operators_trail_their_lines_reports_it(
        self,
    ) -> None:
        # Arrange
        source = """
            is_ready = (
                is_built and
                is_tested and
                is_reviewed
            )
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("boolean-chain-layout", 3)]

    def test_visit_boolop_when_the_first_operand_shares_a_line_reports_it(
        self,
    ) -> None:
        # Arrange
        source = """
            is_ready = (is_built
                and is_tested
                and is_reviewed)
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("boolean-chain-layout", 2)]

    def test_visit_boolop_when_an_operand_is_parenthesised_reads_through(
        self,
    ) -> None:
        # Arrange
        source = """
            is_ready = (
                (is_built or is_cached)
                and is_tested
                and (is_reviewed or is_trivial)
            )
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []


class TestWrappedItemsChecker(HouseRuleTestCase):
    """E9005: wrapped calls, signatures, and collections: one item a line."""

    CHECKER_CLASS = WrappedItemsChecker

    def test_visit_call_when_not_wrapped_reports_nothing(self) -> None:
        # Arrange
        source = "run_gate(gate, command, target=target)"
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_call_when_each_argument_has_a_line_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            run_gate(
                gate,
                command,
                target=target,
            )
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_call_when_two_keywords_share_a_line_reports_it(
        self,
    ) -> None:
        # Arrange
        source = """
            run_gate(
                gate, target=target, strict=True,
            )
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-items-layout", 2)]

    def test_visit_call_when_the_bracket_trails_an_argument_reports_it(
        self,
    ) -> None:
        # Arrange
        source = """
            run_gate(
                gate,
                target)
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-items-layout", 2)]

    def test_visit_list_when_on_one_line_reports_nothing(self) -> None:
        # Arrange
        source = 'gates = ["build", "test"]'
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_list_when_wrapped_one_element_a_line_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            gates = [
                "build",
                "test",
            ]
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_set_when_elements_share_a_line_reports_it(self) -> None:
        # Arrange
        source = """
            keywords = {
                "if", "for",
            }
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-items-layout", 2)]

    def test_visit_tuple_when_elements_share_a_line_reports_it(self) -> None:
        # Arrange
        source = """
            pair = (
                "first", "second",
            )
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-items-layout", 2)]

    def test_visit_dict_when_entries_share_a_line_reports_it(self) -> None:
        # Arrange
        source = """
            levels = {
                "low": 1, "high": 2,
            }
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-items-layout", 2)]

    def test_visit_functiondef_when_parameters_have_lines_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            @cache
            def run(
                gate: str,
                *,
                target: str = "repository",
            ) -> None:
                pass
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_functiondef_when_parameters_share_a_line_reports_it(
        self,
    ) -> None:
        # Arrange
        source = """
            def run(
                gate: str, target: str,
            ) -> None:
                pass
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-items-layout", 2)]

    def test_visit_functiondef_when_the_paren_trails_a_parameter_reports_it(
        self,
    ) -> None:
        # Arrange
        source = """
            async def run(
                gate: str,
                target: str) -> None:
                pass
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-items-layout", 2)]

    def test_visit_functiondef_when_decorated_and_unwrapped_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            @property
            def name(self) -> str:
                return self.value
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_visit_functiondef_when_it_has_no_parameters_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            def run():
                pass
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []


class TestWrappedComprehensionChecker(HouseRuleTestCase):
    """E9006: a wrapped comprehension gives each clause its own line."""

    CHECKER_CLASS = WrappedComprehensionChecker

    def test_check_comprehension_when_on_one_line_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = "names = [gate.name for gate in gates if gate.is_static]"
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_check_comprehension_when_one_clause_a_line_reports_nothing(
        self,
    ) -> None:
        # Arrange
        source = """
            levels = {
                gate.name: gate.level
                for gate in gates
                if gate.is_static
            }
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == []

    def test_check_comprehension_when_clauses_share_a_line_reports_it(
        self,
    ) -> None:
        # Arrange
        source = """
            names = [
                gate.name for gate in gates if gate.is_static
            ]
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-comprehension-layout", 2)]

    def test_check_comprehension_when_an_if_shares_a_line_reports_it(
        self,
    ) -> None:
        # Arrange
        source = """
            total = sum(
                gate.cost
                for gate in gates if gate.is_static
            )
            """
        reported: Reports

        # Act
        reported = self.lint(source)

        # Assert
        assert reported == [("wrapped-comprehension-layout", 2)]


def test_module_lines_when_the_module_has_no_source_returns_no_lines() -> None:
    # Arrange
    module = astroid.nodes.Module("built_without_source")
    lines: list[str]

    # Act
    lines = module_lines(module)

    # Assert
    assert lines == []


def test_def_line_when_the_node_has_no_position_uses_its_start_line() -> None:
    # Arrange
    function = astroid.nodes.FunctionDef(
        "built_without_position",
        lineno=START_LINE,
        col_offset=0,
        parent=None,
        end_lineno=None,
        end_col_offset=None,
    )
    line_number: int

    # Act
    line_number = def_line(function)

    # Assert
    assert line_number == START_LINE
