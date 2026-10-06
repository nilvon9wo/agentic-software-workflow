#!/usr/bin/env bash
# Runs the quality gates with a self-contained Linux toolchain.
#
# On Windows, run this from WSL rather than natively: Smart App Control
# intermittently blocks the test DLLs that coverage instrumentation rewrites,
# and WSL is also the closest local match for the Linux CI runner.
#
# Usage: scripts/gates.sh [run|verify] [gate ...]
#   run     every gate against the repository (the default)
#   verify  every gate against tests/StyleCanary, proving each still fires
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly repository_root
# Outside the checkout: under WSL the checkout is usually on a Windows drive,
# where installing thousands of small files is very slow.
virtual_environment="${XDG_CACHE_HOME:-${HOME}/.cache}/agentic-software-workflow/venv"
readonly virtual_environment

ensure_python_tools() {
  if [[ ! -x "${virtual_environment}/bin/python" ]]; then
    mkdir -p "$(dirname "${virtual_environment}")"
    python3 -m venv "${virtual_environment}"
  fi
  "${virtual_environment}/bin/python" -m pip install --quiet --disable-pip-version-check \
    --requirement "${repository_root}/requirements-dev.txt"
}

main() {
  local -r mode="${1:-run}"
  shift || true
  ensure_python_tools
  export PATH="${virtual_environment}/bin:${PATH}"
  cd "${repository_root}"
  dotnet tool restore >/dev/null
  case "${mode}" in
    run) python scripts/run_gates.py "$@" ;;
    verify) python scripts/verify_gates.py ;;
    *) echo "unknown mode '${mode}': expected run or verify" >&2; return 2 ;;
  esac
}

main "$@"
