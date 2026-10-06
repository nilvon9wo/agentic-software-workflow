"""Tests for scripts/exit_codes.py."""

import exit_codes


def test_exit_code_for_when_successful_returns_zero() -> None:
    # Act
    exit_code: int = exit_codes.exit_code_for(has_succeeded=True)

    # Assert
    assert exit_code == 0


def test_exit_code_for_when_unsuccessful_returns_one() -> None:
    # Act
    exit_code: int = exit_codes.exit_code_for(has_succeeded=False)

    # Assert
    assert exit_code == 1
