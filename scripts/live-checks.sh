#!/usr/bin/env bash
# Runs the live checks: tests marked Explicit that drive a real, authenticated
# `claude` to prove separation of authority holds (see
# tests/AgenticSoftwareWorkflow.Conductor.Test/Live). They spend subscription
# usage, so they never run in CI or in `scripts/gates.sh` - run them on purpose,
# after changing a role's access rules or upgrading Claude Code.
#
# Usage: scripts/live-checks.sh
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly repository_root
readonly test_project="tests/AgenticSoftwareWorkflow.Conductor.Test"
readonly test_assembly="AgenticSoftwareWorkflow.Conductor.Test.dll"

main() {
  if ! command -v claude >/dev/null; then
    echo "claude is not on the PATH: install and sign in to Claude Code first" >&2
    return 2
  fi
  cd "${repository_root}"
  dotnet build "${test_project}" --nologo --verbosity quiet
  local -r output_directory="$(dotnet msbuild "${test_project}" -getProperty:OutputPath)"
  # Run the test assembly directly: `dotnet test` would add the coverage
  # threshold, which a handful of explicit tests cannot meet.
  dotnet "${output_directory}${test_assembly}" \
    --explicit only \
    --filter-trait "Category=Live"
}

main "$@"
