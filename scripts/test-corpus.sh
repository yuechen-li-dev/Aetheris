#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

source "$repo_root/scripts/_dotnet-env.sh"
dotnet_bin="${DOTNET_BIN:-$(resolve_dotnet_bin)}"

AETHERIS_RUN_CORPUS_TESTS=1 "$dotnet_bin" test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj \
  -f net10.0 \
  --filter "FirmamentCirDifferentialAnalysisTests|Category=Corpus|AetherisSuite=Corpus" \
  --logger "console;verbosity=minimal"
