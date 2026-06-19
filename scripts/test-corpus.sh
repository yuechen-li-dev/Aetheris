#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

AETHERIS_RUN_CORPUS_TESTS=1 dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj \
  -f net10.0 \
  --filter "FirmamentCirDifferentialAnalysisTests|Category=Corpus|AetherisSuite=Corpus" \
  --logger "console;verbosity=minimal"
