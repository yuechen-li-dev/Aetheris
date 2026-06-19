#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

unset AETHERIS_RUN_LEGACY_TESTS
unset AETHERIS_RUN_CORPUS_TESTS

dotnet build Aetheris.slnx -f net10.0 --no-restore

dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj -f net10.0 --no-build --logger "console;verbosity=minimal"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --logger "console;verbosity=minimal"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --logger "console;verbosity=minimal"
dotnet test Aetheris.slnx -f net10.0 --no-build --logger "console;verbosity=minimal"
