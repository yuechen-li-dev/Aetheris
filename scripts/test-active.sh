#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

source "$repo_root/scripts/_dotnet-env.sh"
dotnet_bin="${DOTNET_BIN:-$(resolve_dotnet_bin)}"
active_filter="Category!=SlowCorpus"

unset AETHERIS_RUN_LEGACY_TESTS
unset AETHERIS_RUN_CORPUS_TESTS

"$dotnet_bin" build Aetheris.slnx -f net10.0 --no-restore

"$dotnet_bin" test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj -f net10.0 --no-build --filter "$active_filter" --logger "console;verbosity=minimal"
"$dotnet_bin" test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --filter "$active_filter" --logger "console;verbosity=minimal"
"$dotnet_bin" test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "$active_filter" --logger "console;verbosity=minimal"
"$dotnet_bin" test Aetheris.slnx -f net10.0 --no-build --filter "$active_filter" --logger "console;verbosity=minimal"
