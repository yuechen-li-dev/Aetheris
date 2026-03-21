#!/usr/bin/env bash
set -euo pipefail

DOTNET_BIN="${DOTNET_BIN:-dotnet}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

run() {
  printf '+'
  for arg in "$@"; do
    printf ' %q' "$arg"
  done
  printf '\n'
  "$@"
}

cd "$REPO_ROOT"

run_test() {
  local test_project="$1"
  shift || true
  run "$DOTNET_BIN" test "$test_project" --logger "console;verbosity=minimal" "$@"
}

if [ "$#" -gt 0 ]; then
  TEST_PROJECTS=("$@")
  for test_project in "${TEST_PROJECTS[@]}"; do
    run_test "$test_project"
  done
  exit 0
fi

run_test "Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj"
run_test "Aetheris.Server.Tests/Aetheris.Server.Tests.csproj"
run_test "Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj" --filter "FullyQualifiedName~BrepDisplayTessellatorTests"
