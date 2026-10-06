"""Shared harness for testing the house-rule pylint checkers."""

from textwrap import dedent

import astroid  # pyright: ignore[reportMissingTypeStubs] - astroid ships no stubs
from pylint.testutils import CheckerTestCase, MessageTest


class HouseRuleTestCase(CheckerTestCase):
    """Runs one checker over a source snippet and returns its reports."""

    def lint(self, source: str) -> list[tuple[str, int | None]]:
        """The (symbol, line) of each message the checker raises."""
        module = astroid.parse(dedent(source))
        self.walk(module)
        messages: list[MessageTest] = self.linter.release_messages()
        return [(message.msg_id, message.line) for message in messages]
