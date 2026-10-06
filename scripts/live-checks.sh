#!/usr/bin/env bash
# Runs the live checks: tests marked Explicit that drive the real tools - an
# authenticated `claude` (proving separation of authority holds) and `gh`
# (proving the GitHub adapter reads the CLI's real output). See
# tests/AgenticSoftwareWorkflow.Conductor.Test/Live. The claude checks spend
# subscription usage, so none of these run in CI or in `scripts/gates.sh` - run
# them on purpose, after changing a role's access rules, the GitHub adapter, or
# upgrading Claude Code or gh.
#
# Usage: scripts/live-checks.sh
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly repository_root
readonly test_project="tests/AgenticSoftwareWorkflow.Conductor.Test"
readonly test_assembly="AgenticSoftwareWorkflow.Conductor.Test.dll"

main() {
  local tool
  for tool in claude gh; do
    if ! command -v "${tool}" >/dev/null; then
      echo "${tool} is not on the PATH: install and sign in to it first" >&2
      return 2
    fi
  done
  cd "${repository_root}"
  dotnet build "${test_project}" --nologo --verbosity quiet
  local -r output_directory="$(
    dotnet msbuild "${test_project}" -getProperty:OutputPath
  )"
  # Run the test assembly directly: `dotnet test` would add the coverage
  # threshold, which a handful of explicit tests cannot meet.
  dotnet "${output_directory}${test_assembly}" \
    --explicit only \
    --filter-trait "Category=Live"
}

main "$@"
