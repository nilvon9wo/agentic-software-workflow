"""Which gates exist, and the order they run in.

Static gates read the code and docs; the snippet gate runs mdsnippets; the
two test gates run the code. verify_gates.py proves the static gates with
canary markers, the snippet gate with a stale document, and the test gates
with an uncovered addition, so the groups are kept apart.
"""

from gates.configure_await_gate import run_configure_await
from gates.dotnet_gates import run_build, run_format, run_inspect, run_tests
from gates.layout_gate import run_layout
from gates.markdown_gates import run_lychee, run_markdownlint
from gates.model import Gate
from gates.python_gates import (
    run_pylint,
    run_pyright,
    run_python_tests,
    run_ruff,
)
from gates.shell_gates import run_shellcheck
from gates.snippet_gate import run_snippets
from gates.workflow_gate import run_actionlint

STATIC_GATES = (
    Gate("build", run_build),
    Gate("format", run_format),
    Gate("inspect", run_inspect),
    Gate("layout", run_layout),
    Gate("configure-await", run_configure_await),
    Gate("ruff", run_ruff),
    Gate("pylint", run_pylint),
    Gate("pyright", run_pyright),
    Gate("shellcheck", run_shellcheck),
    Gate("markdownlint", run_markdownlint),
    Gate("lychee", run_lychee),
    Gate("actionlint", run_actionlint),
)
SNIPPETS_GATE = Gate("snippets", run_snippets)
TEST_GATE = Gate("test", run_tests)
PYTHON_TEST_GATE = Gate("pytest", run_python_tests)
