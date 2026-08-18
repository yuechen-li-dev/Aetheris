# BUILD-TEST-X1 long-running testhost triage

## Problem statement

After BUILD-TEST-A0 moved the repository to `net10.0` and isolated legacy V1/FrictionLab tests, broad `dotnet test` runs could appear stalled under the minimal console logger. The X1 question was whether tests were truly hanging, whether testhost could not exit, or whether active/legacy/corpus boundaries were still too broad.

## BUILD-TEST-A0 context

A0 made legacy FrictionLab and explicitly legacy Firmament V1 suites opt-in through `AETHERIS_RUN_LEGACY_TESTS=1`. Active CLI, Firmament V2, Core, AIR, and BRep paths remain default.

## Diagnostic commands used

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj -f net10.0 --no-build --blame-hang --blame-hang-timeout 30s --logger "console;verbosity=detailed" --results-directory artifacts/test-diagnostics/cli
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --blame-hang --blame-hang-timeout 30s --logger "console;verbosity=detailed" --results-directory artifacts/test-diagnostics/firmament
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --blame-hang --blame-hang-timeout 30s --logger "console;verbosity=detailed" --results-directory artifacts/test-diagnostics/core
```

The Firmament blame run produced a hang dump at:

```text
artifacts/test-diagnostics/firmament/d16ba4cf-eddd-4a66-b98f-58af55fa7f97/dotnet_6004_20260619T003531_hangdump.dmp
```

and a sequence file at:

```text
artifacts/test-diagnostics/firmament/d16ba4cf-eddd-4a66-b98f-58af55fa7f97/Sequence_510faaa2166b4d599b43e6838c0bb392.xml
```

## Root cause found

The broad run was executing slow corpus/audit work, not failing to exit after all tests finished. The clearest offender was `Aetheris.Kernel.Firmament.Tests.FirmamentCirDifferentialAnalysisTests.CIRvsBRep_DifferentialReportArtifact_IsGeneratedAndReadable`, which was the running test when `--blame-hang-timeout 30s` fired. The preceding `CIRvsBRep_BooleanMatrix` also took about 20 seconds. These tests intentionally run a CIR-vs-BRep differential matrix, estimate volumes, classify probes, and generate/read a JSON report artifact across multiple fixtures.

Core also contains substantial STEP/NIST audit coverage. With detailed blame logging, it completed successfully in about 77 seconds; this is slow corpus-style execution with sparse minimal-console output, not a testhost lifecycle hang.

## Fix applied

The Firmament CIR-vs-BRep differential matrix is now explicit corpus opt-in via `AETHERIS_RUN_CORPUS_TESTS=1`. The test file is excluded from the default Firmament test compile when the environment variable is not set. This keeps the default active suite fast without deleting the corpus tests or weakening assertions.

Added helper scripts:

- `scripts/test-active.sh` runs the default active build/test path.
- `scripts/test-corpus.sh` runs the Firmament CIR-vs-BRep corpus matrix explicitly.
- `scripts/test-diagnose-hang.sh` wraps `dotnet test` with blame-hang diagnostics and deterministic artifact output.

## Reclassified tests

Reclassified as explicit corpus opt-in:

- `Aetheris.Kernel.Firmament.Tests.FirmamentCirDifferentialAnalysisTests`

Reason: the class performs multi-fixture differential audit/report generation and was the identified source of blame-hang inactivity under a 30-second timeout.

## How to run active tests

```bash
scripts/test-active.sh
```

or manually:

```bash
dotnet build Aetheris.slnx -f net10.0 --no-restore
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj -f net10.0 --no-build --logger "console;verbosity=minimal"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --logger "console;verbosity=minimal"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --logger "console;verbosity=minimal"
dotnet test Aetheris.slnx -f net10.0 --no-build --logger "console;verbosity=minimal"
```

## How to run legacy tests

```bash
AETHERIS_RUN_LEGACY_TESTS=1 dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -f net10.0 --filter "CirBoxCylinderConvention" --logger "console;verbosity=minimal"
```

## How to run long-running/corpus tests

```bash
scripts/test-corpus.sh
```

or manually:

```bash
AETHERIS_RUN_CORPUS_TESTS=1 dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --filter "FirmamentCirDifferentialAnalysisTests" --logger "console;verbosity=minimal"
```

## Known remaining limitations

Core STEP/NIST audit tests are still part of the default Core suite and can take more than a minute with minimal logger output. X1 did not reclassify them because the blame run completed successfully and the immediate non-exiting/stalled testhost symptom was isolated to the Firmament CIR-vs-BRep corpus matrix.

## Tests run

See the PR summary for exact validation commands and results.

## Explicit non-goals

X1 did not change geometry behavior, BRep topology behavior, STEP import/export semantics, Firmament V2 syntax/lowering, AIR lowering, artifact emission semantics, CIR behavior, Firmasm, or CAD feature support. The change is limited to test-suite classification, diagnostics scripts, and documentation.
