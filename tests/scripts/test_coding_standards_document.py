"""Tests for the "one non-trivial evaluation per line" rule (AC-1 to AC-10)."""

import re

import pytest

from gates.tools import REPOSITORY_ROOT

CODING_STANDARDS = REPOSITORY_ROOT.joinpath(
    "docs",
    "contribute",
    "coding-standards.md",
)
BULLET_START = "- **One non-trivial evaluation per line"
FORMATTING_PATTERN = re.compile(
    r"\n## Formatting\n.*?(?=\n---|\n## )",
    re.DOTALL,
)
BULLET_PATTERN = re.compile(
    r"- \*\*One non-trivial evaluation per line.*?(?=\n- \*\*|\n---|\Z)",
    re.DOTALL,
)
RULE_FOUR_PATTERN = re.compile(r"\n {2}4\. .*?(?=\n {2}5\. )", re.DOTALL)
CROSS_LANGUAGE_PATTERN = re.compile(
    r"The house rules above carry over.*?(?=\n\n)",
    re.DOTALL,
)
FREE_SENTENCE_PATTERN = re.compile(r"[^.]*`await`[^.]*")
ENFORCEMENT_CLAIM_PATTERN = re.compile(
    r"(?<!will be )enforced by"
    r"|(is|are|currently|already)\s+(enforced|checked|gated)"
    r"|(gate|analyzer)s?\s+(checks|enforces|catches|verifies)",
    re.IGNORECASE,
)
OLD_BULLET = "One expression per line; one variable declaration per line."


class SectionMissingError(LookupError):
    """The coding standards lack a section or bullet the tests read."""


def document() -> str:
    """The whole text of the coding standards."""
    return CODING_STANDARDS.read_text(encoding="utf-8")


def formatting_section() -> str:
    """The text of the "Formatting" section."""
    match = FORMATTING_PATTERN.search(document())
    if match is None:
        raise SectionMissingError
    else:
        return match.group(0)


def evaluation_bullet() -> str:
    """The text of the "One non-trivial evaluation per line" bullet."""
    match = BULLET_PATTERN.search(formatting_section())
    if match is None:
        raise SectionMissingError
    else:
        return match.group(0)


def rule_four() -> str:
    """The text of house rule 4."""
    match = RULE_FOUR_PATTERN.search(document())
    if match is None:
        raise SectionMissingError
    else:
        return match.group(0)


def cross_language_sentence() -> str:
    """The text of the cross-language paragraph that lists the house rules."""
    match = CROSS_LANGUAGE_PATTERN.search(document())
    if match is None:
        raise SectionMissingError
    else:
        return match.group(0)


def free_construct_sentence() -> str:
    """The sentence of the evaluation bullet that lists the free constructs."""
    match = FREE_SENTENCE_PATTERN.search(evaluation_bullet())
    if match is None:
        raise SectionMissingError
    else:
        return match.group(0)


def rule_text_that_could_claim_enforcement() -> str:
    """Every place that states the rule, with whitespace normalised."""
    places: list[str] = [
        rule_four(),
        evaluation_bullet(),
        cross_language_sentence(),
    ]
    return " ".join(" ".join(places).split())


def test_formatting_section_when_read_has_exactly_one_evaluation_bullet() -> (
    None
):
    # Arrange
    section: str = formatting_section()

    # Act
    count: int = section.count(BULLET_START)

    # Assert
    assert count == 1  # AC-1


def test_evaluation_bullet_when_read_says_at_most_one_evaluation() -> None:
    # Arrange
    pattern = re.compile(r"at\s+most\s+one\s+non-trivial", re.IGNORECASE)

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-1


def test_evaluation_bullet_when_read_keeps_one_variable_per_declaration() -> (
    None
):
    # Arrange
    pattern = re.compile(
        r"declaration.*one\s+variable\s+per\s+line",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-1


@pytest.mark.parametrize(
    "construct",
    [
        r"`await`",
        r"\bcasts\b",
        r"\bliterals\b",
        r"\bnames\b",
        r"unary\s+`!`\s+and\s+`-`",
        r"`not`",
        r"`nameof\(\)`",
        r"`typeof\(\)`",
        r"short\s+property\s+access",
    ],
)
def test_free_construct_sentence_when_read_lists_each_free_construct(
    construct: str,
) -> None:
    # Arrange
    pattern = re.compile(construct, re.IGNORECASE)

    # Act
    sentence: str = free_construct_sentence()

    # Assert
    assert pattern.search(sentence) is not None  # AC-2


def test_free_construct_sentence_when_read_says_they_are_not_counted() -> None:
    # Arrange
    pattern = re.compile(r"free|not\s+count", re.IGNORECASE)

    # Act
    sentence: str = free_construct_sentence()

    # Assert
    assert pattern.search(sentence) is not None  # AC-2


def test_evaluation_bullet_when_read_says_plain_dots_are_not_counted() -> None:
    # Arrange
    pattern = re.compile(
        r"plain\s+member\s+access.*not\s+counted",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-3


def test_evaluation_bullet_when_read_keeps_the_receiver_call_whole() -> None:
    # Arrange
    pattern = re.compile(
        r"`this\._processRunner\.ReceivedCalls\(\)`\s+"
        r"(stays|remains)\s+(together|whole|on\s+one\s+line)",
        re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-3


def test_evaluation_bullet_when_read_splits_a_chain_of_two_calls() -> None:
    # Arrange
    pattern = re.compile(r"^( *)x\.Foo\(\)\n\1 {4}\.Bar\(\)", re.MULTILINE)

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-3


def test_evaluation_bullet_when_read_shows_the_split_in_csharp() -> None:
    # Arrange
    pattern = re.compile(
        r"```csharp\n(?:(?!```).)*?^( *)x\.Foo\(\)\n\1 {4}\.Bar\(\)",
        re.DOTALL | re.MULTILINE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-3


def test_evaluation_bullet_when_read_puts_later_calls_on_own_lines() -> None:
    # Arrange
    pattern = re.compile(
        r"each\s+call.*after\s+the\s+first.*own\s+line",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-3


def test_evaluation_bullet_when_read_indents_a_call_chain_one_level() -> None:
    # Arrange
    pattern = re.compile(
        r"after\s+the\s+first.*dot\s+first.*one\s+level\s+past"
        r"\s+the\s+start\s+of\s+the\s+chain",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-3


def test_evaluation_bullet_when_read_puts_the_dot_first() -> None:
    # Arrange
    pattern = re.compile(r"dot\s+first", re.IGNORECASE)

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-3


def test_evaluation_bullet_when_read_defines_a_long_access_chain() -> None:
    # Arrange
    pattern = re.compile(
        r"three\s+or\s+more\s+consecutive\s+member\s+accesses",
        re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-4


def test_evaluation_bullet_when_read_breaks_a_long_chain_per_member() -> None:
    # Arrange
    pattern = re.compile(
        r"long.*one\s+`\.member`\s+per\s+line",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-4


def test_evaluation_bullet_when_read_indents_a_long_chain_one_level() -> None:
    # Arrange
    pattern = re.compile(
        r"one\s+`\.member`\s+per\s+line.*dot\s+first.*one\s+level\s+past"
        r"\s+the\s+start\s+of\s+the\s+chain",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-4


def test_evaluation_bullet_when_read_always_breaks_null_operators() -> None:
    # Arrange
    pattern = re.compile(
        r"`\?\.`.*`\?\?`.*always.*own\s+line",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-5


def test_evaluation_bullet_when_read_indents_null_operators_one_level() -> (
    None
):
    # Arrange
    pattern = re.compile(
        r"`\?\?`.*start\s+of\s+its\s+own\s+line.*one\s+level\s+past"
        r"\s+the\s+start\s+of\s+the\s+statement",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-5


def test_evaluation_bullet_when_read_shows_the_null_operator_layout() -> None:
    # Arrange
    pattern = re.compile(
        r"^( *)string a = foo\n"
        r"\1 {4}\?\.bar\n"
        r"\1 {4}\?\.baz\n"
        r"\1 {4}\?\? \"whatever\";",
        re.MULTILINE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-5


def test_evaluation_bullet_when_read_breaks_a_ternary_in_three_lines() -> None:
    # Arrange
    pattern = re.compile(
        r"ternary.*three\s+lines.*condition.*\? whenTrue.*: whenFalse",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-6


def test_evaluation_bullet_when_read_indents_a_nested_ternary_further() -> (
    None
):
    # Arrange
    pattern = re.compile(
        r"nested.*false\s+branch.*one\s+further\s+level",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-6


def test_evaluation_bullet_when_read_shows_the_nested_ternary_layout() -> None:
    # Arrange
    pattern = re.compile(
        r"^( *)string b = isFoo\(\)\n"
        r"\1 {4}\? \"A\"\n"
        r"\1 {4}: isBar\(\)\n"
        r"\1 {8}\? \"B\"\n"
        r"\1 {8}: \"C\";",
        re.MULTILINE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-6


def test_evaluation_bullet_when_read_says_dots_are_indented_not_aligned() -> (
    None
):
    # Arrange
    pattern = re.compile(
        r"indented,\s+not\s+aligned",
        re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-7


def test_evaluation_bullet_when_read_gives_the_reason_for_not_aligning() -> (
    None
):
    # Arrange
    pattern = re.compile(r"custom\s+formatter", re.IGNORECASE)

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-7


def test_evaluation_bullet_when_read_applies_to_every_language() -> None:
    # Arrange
    pattern = re.compile(r"every\s+language", re.IGNORECASE)

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-8


def test_evaluation_bullet_when_read_defers_to_each_language_syntax() -> None:
    # Arrange
    pattern = re.compile(r"own\s+syntax", re.IGNORECASE)

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-8


def test_rule_four_when_read_refers_to_the_new_bullet() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    rule: str = rule_four()

    # Assert
    assert "One non-trivial evaluation per line" in rule  # AC-9


def test_rule_four_when_read_drops_the_old_wording() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    rule: str = rule_four()

    # Assert
    assert "Never more than one expression per line." not in rule  # AC-9


def test_cross_language_sentence_when_read_refers_to_the_new_bullet() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    sentence: str = cross_language_sentence()

    # Assert
    assert "One non-trivial evaluation per line" in sentence  # AC-9


def test_document_when_read_drops_the_old_formatting_bullet() -> None:
    # Arrange
    # Nothing to arrange: the document is read in the Act.

    # Act
    text: str = document()

    # Assert
    assert OLD_BULLET not in text  # AC-9


def test_document_when_read_states_no_other_one_expression_rule() -> None:
    # Arrange
    pattern = re.compile(r"one\s+expression\s+per\s+line", re.IGNORECASE)

    # Act
    text: str = document()

    # Assert
    assert pattern.search(text) is None  # AC-9


def test_rule_text_when_read_does_not_claim_the_rule_is_gated() -> None:
    # Arrange
    text: str = rule_text_that_could_claim_enforcement()

    # Act
    claim: re.Match[str] | None = ENFORCEMENT_CLAIM_PATTERN.search(text)

    # Assert
    assert claim is None  # AC-10


def test_evaluation_bullet_when_read_says_the_analyzer_in_3_will_enforce() -> (
    None
):
    # Arrange
    pattern = re.compile(
        r"will\s+be\s+enforced.*analyzer.*#3",
        re.DOTALL | re.IGNORECASE,
    )

    # Act
    bullet: str = evaluation_bullet()

    # Assert
    assert pattern.search(bullet) is not None  # AC-10
