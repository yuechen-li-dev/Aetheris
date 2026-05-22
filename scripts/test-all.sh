#!/usr/bin/env bash
set -euo pipefail

DOTNET_BIN="${DOTNET_BIN:-dotnet}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PRIMARY_TFM="${AETHERIS_PRIMARY_TFM:-net10.0}"

run() {
  printf '+'
  for arg in "$@"; do
    printf ' %q' "$arg"
  done
  printf '\n'
  "$@"
}

cd "$REPO_ROOT"

if [ "$#" -gt 0 ]; then
  for test_project in "$@"; do
    run "$DOTNET_BIN" test "$test_project" -f "$PRIMARY_TFM" --logger "console;verbosity=minimal"
  done
  exit 0
fi

run "$DOTNET_BIN" test Aetheris.slnx -f "$PRIMARY_TFM" --logger "console;verbosity=minimal" --filter "Category!=SlowCorpus"
