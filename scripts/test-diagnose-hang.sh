#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "usage: scripts/test-diagnose-hang.sh <project-or-solution> [additional dotnet test args...]" >&2
  exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

source "$repo_root/scripts/_dotnet-env.sh"
dotnet_bin="${DOTNET_BIN:-$(resolve_dotnet_bin)}"

subject="$1"
shift
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
results_dir="artifacts/test-diagnostics/hang-$stamp"
mkdir -p "$results_dir"

"$dotnet_bin" test "$subject" -f net10.0 --no-build \
  --blame-hang --blame-hang-timeout "${AETHERIS_BLAME_HANG_TIMEOUT:-120s}" \
  --logger "console;verbosity=detailed" \
  --results-directory "$results_dir" \
  "$@"
