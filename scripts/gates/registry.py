"""Which gates exist, and the order they run in.

Static gates read the code; the two test gates run it. verify_gates.py
proves the static gates with canary markers and the test gates with an
uncovered addition, so the two groups are kept apart.
"""

from gates.dotnet_gates import run_build, run_format, run_inspect, run_tests
from gates.layout_gate import run_layout
from gates.model import Gate
from gates.python_gates import (
    run_pylint,
    run_pyright,
    run_python_tests,
    run_ruff,
)
from gates.shell_gates import run_shellcheck

STATIC_GATES = (
    Gate("build", run_build),
    Gate("format", run_format),
    Gate("inspect", run_inspect),
    Gate("layout", run_layout),
    Gate("ruff", run_ruff),
    Gate("pylint", run_pylint),
    Gate("pyright", run_pyright),
    Gate("shellcheck", run_shellcheck),
)
TEST_GATE = Gate("test", run_tests)
PYTHON_TEST_GATE = Gate("pytest", run_python_tests)
