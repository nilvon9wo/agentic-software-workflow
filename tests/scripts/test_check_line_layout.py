"""Tests for scripts/check_line_layout.py."""

from pathlib import Path

import pytest

import check_line_layout
from check_line_layout import LayoutViolation

LONG_LINE = "x" * 121
WRAPPED_CALL_CLOSED_ON_ITS_OWN_LINE = "Call(\n    first,\n    second\n);\n"
WRAPPED_CALL_CLOSED_AFTER_AN_ARGUMENT = "Call(\n    first,\n    second);\n"


def write_source(
    directory: Path,
    source: str,
    file_name: str = "Sample.cs",
) -> Path:
    """Write C# source to a file and return its path."""
    path = directory / file_name
    path.write_text(source, encoding="utf-8")
    return path


def rules_in(violations: list[LayoutViolation]) -> list[tuple[str, int]]:
    """The (rule, line) of each violation."""
    return [(violation.rule, violation.line_number) for violation in violations]


def test_check_file_when_a_line_exceeds_the_maximum_reports_it(
    tmp_path: Path,
) -> None:
    # Arrange
    path = write_source(tmp_path, f"ok\n{LONG_LINE}\n")
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_file(path)

    # Assert
    assert rules_in(violations) == [("line-length", 2)]


def test_check_file_when_a_line_is_exactly_the_maximum_reports_nothing(
    tmp_path: Path,
) -> None:
    # Arrange
    path = write_source(tmp_path, "x" * 120)
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_file(path)

    # Assert
    assert violations == []


def test_check_file_when_a_wrapped_call_closes_on_its_own_line_reports_nothing(
    tmp_path: Path,
) -> None:
    # Arrange
    path = write_source(tmp_path, WRAPPED_CALL_CLOSED_ON_ITS_OWN_LINE)
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_file(path)

    # Assert
    assert violations == []


def test_check_file_when_a_wrapped_call_closes_after_an_argument_reports_it(
    tmp_path: Path,
) -> None:
    # Arrange
    path = write_source(tmp_path, WRAPPED_CALL_CLOSED_AFTER_AN_ARGUMENT)
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_file(path)

    # Assert
    assert rules_in(violations) == [("wrap-rpar", 3)]


def test_check_file_when_a_control_keyword_condition_wraps_reports_nothing(
    tmp_path: Path,
) -> None:
    # Arrange
    path = write_source(tmp_path, "if (first &&\n    second)\n{\n}\n")
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_file(path)

    # Assert
    assert violations == []


def test_check_file_when_a_wrapped_paren_follows_no_name_reports_nothing(
    tmp_path: Path,
) -> None:
    # Arrange
    path = write_source(tmp_path, "int total = (first +\n    second);\n")
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_file(path)

    # Assert
    assert violations == []


@pytest.mark.parametrize(
    "literal",
    [
        '"Call(\n"',
        "// Call(\n",
        "/* Call(\n x); */",
        '@"Call(\n x);"',
        '"""\nCall(\n x);\n"""',
        "')'",
    ],
)
def test_check_file_when_parens_are_inside_a_literal_or_comment_ignores_them(
    tmp_path: Path,
    literal: str,
) -> None:
    # Arrange
    path = write_source(tmp_path, f"string text = {literal};\n")
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_file(path)

    # Assert
    assert violations == []


def test_check_file_when_a_close_paren_has_no_opener_ignores_it(
    tmp_path: Path,
) -> None:
    # Arrange
    path = write_source(tmp_path, ")\n")
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_file(path)

    # Assert
    assert violations == []


def test_check_paths_when_given_a_directory_skips_build_output_and_the_canary(
    tmp_path: Path,
) -> None:
    # Arrange
    for excluded in ("bin", "obj", "StyleCanary"):
        excluded_directory = tmp_path / excluded
        excluded_directory.mkdir()
        write_source(excluded_directory, LONG_LINE)
    checked = write_source(tmp_path, LONG_LINE)
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_paths([tmp_path])

    # Assert
    assert [violation.path for violation in violations] == [checked]


def test_check_paths_when_given_a_file_checks_it_even_inside_the_canary(
    tmp_path: Path,
) -> None:
    # Arrange
    canary_directory = tmp_path / "StyleCanary"
    canary_directory.mkdir()
    canary_file = write_source(canary_directory, LONG_LINE)
    violations: list[LayoutViolation]

    # Act
    violations = check_line_layout.check_paths([canary_file])

    # Assert
    assert rules_in(violations) == [("line-length", 1)]


def test_describe_when_called_formats_the_violation_for_editors_and_logs() -> (
    None
):
    # Arrange
    violation = LayoutViolation(Path("Sample.cs"), 7, "wrap-rpar", "message")
    description: str

    # Act
    description = violation.describe()

    # Assert
    assert description == "Sample.cs:7: wrap-rpar: message"


def test_to_roots_when_given_no_arguments_returns_the_default_roots() -> None:
    # Arrange
    roots: list[Path]

    # Act
    roots = check_line_layout.to_roots([])

    # Assert
    assert roots == [Path("src"), Path("tests")]


def test_to_roots_when_given_arguments_returns_them_as_paths() -> None:
    # Arrange
    roots: list[Path]

    # Act
    roots = check_line_layout.to_roots(["one", "two"])

    # Assert
    assert roots == [Path("one"), Path("two")]


def test_main_when_there_are_violations_prints_them_and_fails(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    # Arrange
    path = write_source(tmp_path, LONG_LINE)
    exit_code: int

    # Act
    exit_code = check_line_layout.main([str(path)])

    # Assert
    assert (exit_code, capsys.readouterr().out) == (
        1,
        f"{path}:1: line-length: 121 characters (max 120)\n",
    )


def test_main_when_there_are_no_violations_prints_ok_and_succeeds(
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    # Arrange
    path = write_source(tmp_path, "ok\n")
    exit_code: int

    # Act
    exit_code = check_line_layout.main([str(path)])

    # Assert
    assert (exit_code, capsys.readouterr().out) == (0, "line layout: OK\n")
