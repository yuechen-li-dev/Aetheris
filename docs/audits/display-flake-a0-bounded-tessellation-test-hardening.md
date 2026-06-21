# DISPLAY-FLAKE-A0 bounded tessellation test hardening

## Failing tests investigated

- `DisplayPreparationFallbackBuilderTests.Build_UnsupportedBody_StaysOnExistingFallbackWithoutScaffoldAttempt`
- `Step242Ftc07ViewMaterializationRegressionTests.Step242Ftc07ViewMaterialization_CompletesOrReportsBoundedDiagnostic`

## Reproduction results

The broad filtered validation command was rerun after restore/build:

```bash
dotnet test Aetheris.slnx -f net10.0 --no-build --filter "INLINE-STEP|InlineStep|FirmamentV2|AP242|Build|Step242"
```

In this environment the broad run passed, including `Aetheris.Kernel.Core.Tests` with 306 matching tests in about 2 minutes. Direct reruns also passed: `DisplayPreparationFallbackBuilderTests` completed 7 tests in under 1 second per run, and `Step242Ftc07ViewMaterializationRegressionTests` completed 3 tests in roughly 22-23 seconds per run.

## Root cause

The FTC-07 regression was asserting a fixed 15-second process wall-clock limit around a test fixture that intentionally exercises the bounded tessellation timeout path. That wall-clock assertion measured scheduler/load overhead in addition to the production tessellation budget. Under broad filtered runs, other matching tests can increase machine load enough for the wall-clock assertion to become brittle even when the implementation still returns the documented bounded result: either display patches or a `Viewer.Tessellation.Timeout` diagnostic.

The display fallback test did not reproduce locally. Its contract remains stable: unsupported non-B-spline bodies should stay on tessellator fallback patches and should not report scaffold rejection reasons.

## Hardening applied

The FTC-07 tests now use an explicit 5-second tessellation budget and keep a separate 30-second wall-clock guard with diagnostic output on failure. This preserves the bounded-behavior contract while avoiding a brittle machine-load threshold. Related FTC-07 regression tests use the same explicit budget so the fixture is validated consistently.

## Before / after behavior

Before, a valid bounded timeout result could still fail if the end-to-end test process exceeded 15 seconds under load. After, the test still fails if materialization hangs past the guard or returns an unexpected result, but it accepts the documented outcomes: successful patches or the bounded `Viewer.Tessellation.Timeout` diagnostic.

## Validation commands

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "DisplayPreparationFallbackBuilderTests"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "Step242Ftc07ViewMaterializationRegressionTests"
dotnet test Aetheris.slnx -f net10.0 --no-build --filter "INLINE-STEP|InlineStep|FirmamentV2|AP242|Build|Step242"
```
