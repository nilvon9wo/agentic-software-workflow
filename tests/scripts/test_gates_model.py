"""Tests for scripts/gates/model.py."""

from gates.model import Finding, GateResult

A_FINDING = Finding("markdownlint", "MD034", "README.md", 3)


def test_has_passed_when_clean_and_nothing_reported_is_true() -> None:
    # Arrange
    result = GateResult("markdownlint", 0, [], "")

    # Act
    has_passed: bool = result.has_passed

    # Assert
    assert has_passed


def test_has_passed_when_the_tool_exits_non_zero_is_false() -> None:
    # Arrange
    result = GateResult("markdownlint", 1, [], "")

    # Act
    has_passed: bool = result.has_passed

    # Assert
    assert not has_passed


def test_has_passed_when_anything_is_reported_is_false() -> None:
    # Arrange
    result = GateResult("markdownlint", 0, [A_FINDING], "")

    # Act
    has_passed: bool = result.has_passed

    # Assert
    assert not has_passed
