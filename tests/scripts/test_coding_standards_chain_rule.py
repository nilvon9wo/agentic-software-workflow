"""Tests for the chained-call rule in docs/contribute/coding-standards.md."""

import re
import textwrap
from pathlib import Path

STANDARDS = (
    Path(__file__).resolve().parents[2]
    / "docs"
    / "contribute"
    / "coding-standards.md"
)
SECTION_START = "## Formatting"
SECTION_END = "\n---"
BULLET_TITLE = "- **One expression per line"
BULLET_MARKER = "- "
EXPRESSION_RULE = "one expression per line"
CSHARP_FENCE = re.compile(r"```csharp\n(.*?)```", re.DOTALL)
SENTENCE_BREAK = re.compile(r"(?<=[.!?])\s+")
LAYOUT_GATE = re.compile(r"`?layout`?\s+gate", re.IGNORECASE)
RECEIVED_CALLS = "this._processRunner.ReceivedCalls()"
KEPT_WHOLE_PHRASES = ("is not split", "stays together")
FUTURE_ENFORCEMENT = "will be enforced"
TWO_CALL_CHAIN = "x.Foo()\n    .Bar()"
LAYOUT_ANALYZER_ISSUE = "#3"


def formatting_section() -> str:
    """The text of the `## Formatting` section."""
    text: str = STANDARDS.read_text(encoding="utf-8")
    start: int = text.index(SECTION_START)
    end: int = text.index(SECTION_END, start)
    return text[start:end]


def section_bullets() -> list[str]:
    """The top-level bullets of the Formatting section, each as raw text."""
    bullets: list[str] = []
    for line in formatting_section().splitlines():
        if line.startswith(BULLET_MARKER):
            bullets.append(line)
        elif bullets:
            bullets[-1] += "\n" + line
    return bullets


def chain_bullet() -> str:
    """The bullet titled "One expression per line", as raw text."""
    titled: list[str] = [
        bullet
        for bullet in section_bullets()
        if bullet.startswith(BULLET_TITLE)
    ]
    assert len(titled) == 1
    return titled[0]


def chain_prose() -> str:
    """The chain bullet with its wrapping collapsed, for phrase matching."""
    return " ".join(chain_bullet().split())


def chain_example() -> str:
    """The dedented `csharp` fenced block inside the chain bullet."""
    blocks: list[str] = CSHARP_FENCE.findall(chain_bullet())
    assert len(blocks) == 1
    return textwrap.dedent(blocks[0])


def chain_sentences() -> list[str]:
    """The chain bullet's prose as sentences, ignoring dots inside code."""
    prose: str = CSHARP_FENCE.sub(" ", chain_bullet())
    collapsed: str = " ".join(prose.split())
    return SENTENCE_BREAK.split(collapsed)


def enforcement_sentences() -> list[str]:
    """Sentences of the chain bullet that mention enforcement, any case."""
    return [
        sentence
        for sentence in chain_sentences()
        if "enforce" in sentence.lower()
    ]


# AC-1
def test_formatting_when_read_has_the_expression_bullet() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    bullet: str = chain_bullet()

    # Assert
    assert bullet.startswith(BULLET_TITLE)


# AC-1
def test_chain_when_read_puts_later_calls_on_own_lines() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    prose: str = chain_prose().lower()

    # Assert
    assert "every call after the first goes on its own line" in prose


# AC-1
def test_chain_when_read_indents_one_level() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    prose: str = chain_prose().lower()

    # Assert
    assert "indented one level past the start of the chain" in prose


# AC-1
def test_chain_when_read_puts_the_dot_first() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    prose: str = chain_prose().lower()

    # Assert
    assert "dot first" in prose


# AC-2
def test_chain_when_read_keeps_member_access_whole() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    prose: str = chain_prose().lower()

    # Assert
    assert "member access before the first call is not split" in prose


# AC-2
def test_chain_when_read_gives_received_calls_example() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    examples: list[str] = [
        sentence
        for sentence in chain_sentences()
        if RECEIVED_CALLS in sentence
    ]

    # Assert
    assert len(examples) > 0


# AC-2
def test_chain_example_when_read_is_said_to_stay_together() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    misplaced: list[str] = [
        sentence
        for sentence in chain_sentences()
        if RECEIVED_CALLS in sentence
        and not any(phrase in sentence for phrase in KEPT_WHOLE_PHRASES)
    ]

    # Assert
    assert misplaced == []


# AC-3
def test_chain_when_read_has_exactly_one_csharp_example() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    blocks: list[str] = CSHARP_FENCE.findall(chain_bullet())

    # Assert
    assert len(blocks) == 1


# AC-3
def test_chain_example_when_read_indents_the_second_call_four_spaces() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    example: str = chain_example()

    # Assert
    assert TWO_CALL_CHAIN in example


# AC-4
def test_chain_when_read_says_dots_are_indented_not_aligned() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    prose: str = chain_prose().lower()

    # Assert
    assert "not aligned" in prose


# AC-4
def test_chain_when_read_gives_the_custom_formatter_reason() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    prose: str = chain_prose().lower()

    # Assert
    assert "custom formatter" in prose


# AC-5
def test_formatting_when_read_states_the_rule_in_one_bullet() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    stating: list[str] = [
        bullet
        for bullet in section_bullets()
        if EXPRESSION_RULE in bullet.lower()
    ]

    # Assert
    assert len(stating) == 1


# AC-5
def test_chain_when_read_keeps_one_declaration_per_line() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    prose: str = chain_prose().lower()

    # Assert
    assert "one variable declaration per line" in prose


# AC-8
def test_chain_when_enforcement_is_mentioned_cites_issue_three() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    uncited: list[str] = [
        sentence
        for sentence in enforcement_sentences()
        if LAYOUT_ANALYZER_ISSUE not in sentence
    ]

    # Assert
    assert uncited == []


# AC-8
def test_chain_when_enforcement_is_mentioned_uses_future_tense() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    current: list[str] = [
        sentence
        for sentence in enforcement_sentences()
        if FUTURE_ENFORCEMENT not in sentence.lower()
    ]

    # Assert
    assert current == []


# AC-8
def test_chain_when_enforcement_is_mentioned_names_no_layout_gate() -> None:
    # Arrange
    # Nothing to arrange: the standards document is read from the repository.

    # Act
    gated: list[str] = [
        sentence
        for sentence in enforcement_sentences()
        if LAYOUT_GATE.search(sentence)
    ]

    # Assert
    assert gated == []
