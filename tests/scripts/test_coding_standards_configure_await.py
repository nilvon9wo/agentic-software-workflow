"""Tests for the ConfigureAwait bullet of the coding standards."""

import re

from gates.tools import REPOSITORY_ROOT

CODING_STANDARDS = (
    REPOSITORY_ROOT
    / "docs"
    / "contribute"
    / "coding-standards.md"
)
BULLET_PATTERN = re.compile(
    r"- \*\*No `ConfigureAwait` noise\.\*\*.*?(?=\n- \*\*|\n---)",
    re.DOTALL,
)


class BulletMissingError(LookupError):
    """The coding standards have no ConfigureAwait bullet."""


def configure_await_bullet() -> str:
    """The text of the "No `ConfigureAwait` noise" bullet."""
    text = CODING_STANDARDS.read_text(encoding="utf-8")
    match = BULLET_PATTERN.search(text)
    if match is None:
        raise BulletMissingError
    else:
        return match.group(0)


def test_configure_await_bullet_when_read_is_not_the_old_claim() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "does nothing" not in bullet


def test_configure_await_bullet_when_read_drops_the_console_claim() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "console processes" not in bullet


def test_configure_await_bullet_when_read_drops_the_not_needed_claim() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "does not need" not in bullet


def test_configure_await_bullet_when_read_says_every_project_declares_it() -> (
    None
):
    # Arrange
    # Nothing to arrange: the document is read in the Act.
    pattern = re.compile(
        r"every\s+project.*GlobalSuppressions\.cs",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert pattern.search(bullet) is not None


def test_configure_await_bullet_when_read_states_the_fody_attribute() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "[assembly: Fody.ConfigureAwait(false)]" in bullet


def test_configure_await_bullet_when_read_names_the_file() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "GlobalSuppressions.cs" in bullet


def test_configure_await_bullet_when_read_names_the_fody_package() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "ConfigureAwait.Fody" in bullet


def test_configure_await_bullet_when_read_says_never_written() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "never written" in bullet


def test_configure_await_bullet_when_read_names_the_enforcer_rule() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "ConfigureAwaitEnforcer" in bullet


def test_configure_await_bullet_when_read_names_the_ca2007_rule() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "CA2007" in bullet


def test_configure_await_bullet_when_read_names_the_reference() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    bullet: str = configure_await_bullet()

    # Assert
    assert "PackageReference" in bullet
