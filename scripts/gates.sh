#!/usr/bin/env bash
# Runs the quality gates with a self-contained Linux toolchain: C#, Python,
# shell, Markdown (lint, links, tested snippets) and GitHub workflows. This is
# the whole definition of done - CI runs exactly this, and nothing more.
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
tool_cache="${XDG_CACHE_HOME:-${HOME}/.cache}/agentic-software-workflow"
readonly tool_cache
readonly virtual_environment="${tool_cache}/venv"
readonly node_environment="${tool_cache}/node"
readonly lychee_directory="${tool_cache}/lychee"

# Pinned to the versions the former CI actions used. lychee has no package on
# PyPI or npm, so its release archive is downloaded and checked against the
# checksum GitHub publishes for it.
readonly node_version="24.21.0"
readonly markdownlint_version="0.23.2"
readonly lychee_version="0.24.2"
readonly lychee_build="lychee-x86_64-unknown-linux-musl"
readonly lychee_sha256="73657a111819a30c47c08352896796f23d64e4eb2b3ed39b6d32149241566fc5"

require() {
  local -r command_name="$1"
  if ! command -v "${command_name}" >/dev/null; then
    echo "gates.sh needs '${command_name}', which is not installed." >&2
    exit 1
  fi
}

ensure_python_tools() {
  if [[ ! -x "${virtual_environment}/bin/python" ]]; then
    mkdir -p "${tool_cache}"
    python3 -m venv "${virtual_environment}"
  fi
  "${virtual_environment}/bin/python" -m pip install --quiet --disable-pip-version-check \
    --requirement "${repository_root}/requirements-dev.txt"
}

# A private Node, so a system Node (or, under WSL, Windows' Node leaking in
# through PATH) can neither be required nor interfere.
ensure_markdownlint() {
  local -r stamp="${node_environment}/.markdownlint-cli2-${markdownlint_version}"
  local -r node="${node_environment}/bin/node"
  if [[ ! -x "${node}" ]] || ! "${node}" --version | grep -qx "v${node_version}"; then
    rm -rf "${node_environment}"
    "${virtual_environment}/bin/nodeenv" --quiet --prebuilt --node="${node_version}" "${node_environment}"
  fi
  if [[ ! -f "${stamp}" ]]; then
    PATH="${node_environment}/bin:${PATH}" "${node_environment}/bin/npm" install --global --silent \
      "markdownlint-cli2@${markdownlint_version}"
    touch "${stamp}"
  fi
}

ensure_lychee() {
  local -r installed="${lychee_directory}/${lychee_version}/lychee"
  if [[ ! -x "${installed}" ]]; then
    require curl
    require sha256sum
    local -r download="$(mktemp)"
    curl --fail --silent --show-error --location --output "${download}" \
      "https://github.com/lycheeverse/lychee/releases/download/lychee-v${lychee_version}/${lychee_build}.tar.gz"
    if ! echo "${lychee_sha256}  ${download}" | sha256sum --check --status; then
      rm -f "${download}"
      echo "lychee ${lychee_version} download does not match its pinned checksum." >&2
      exit 1
    fi
    mkdir -p "${lychee_directory}/${lychee_version}"
    tar --extract --gzip --file "${download}" --directory "${lychee_directory}/${lychee_version}" \
      --strip-components=1 "${lychee_build}/lychee"
    rm -f "${download}"
  fi
  ln -sf "${installed}" "${virtual_environment}/bin/lychee"
}

main() {
  local -r mode="${1:-run}"
  shift || true
  require python3
  require dotnet
  ensure_python_tools
  ensure_markdownlint
  ensure_lychee
  export PATH="${virtual_environment}/bin:${node_environment}/bin:${PATH}"
  cd "${repository_root}"
  dotnet tool restore >/dev/null
  case "${mode}" in
    run) python scripts/run_gates.py "$@" ;;
    verify) python scripts/verify_gates.py ;;
    *) echo "unknown mode '${mode}': expected run or verify" >&2; return 2 ;;
  esac
}

main "$@"
