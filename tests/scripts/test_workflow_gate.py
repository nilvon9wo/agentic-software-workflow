"""Tests for scripts/gates/workflow_gate.py."""

import json
from pathlib import Path

import pytest
from gate_testing import a_target, a_tool_printing

from gates import workflow_gate
from gates.model import Finding, GateResult

CI = Path(".github") / "workflows" / "ci.yml"
ACTIONLINT_REPORT = [
    {"filepath": ".github/workflows/ci.yml", "kind": "expression", "line": 11},
]


def test_run_actionlint_when_a_workflow_is_wrong_reports_each_error(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    report = json.dumps(ACTIONLINT_REPORT)
    tool = a_tool_printing(report, exit_code=1)
    monkeypatch.setattr(workflow_gate, "execute", tool)

    # Act
    result: GateResult = workflow_gate.run_actionlint(
        a_target(workflow_paths=[CI]),
    )

    # Assert
    assert result.findings == [
        Finding("actionlint", "expression", "ci.yml", 11),
    ]
