# STEP242-TEST-HANG-A0 broad-filter hardening

## Failing command

```bash
dotnet test Aetheris.slnx -f net10.0 --no-build --filter "INLINE-STEP|InlineStep|PMI|pmi|FirmamentV2|AP242|Build|Step242" --logger "console;verbosity=normal"
```

## Reproduction method and observed result

After `dotnet restore Aetheris.slnx` and `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`, the broad solution-level filter was rerun with normal console logging and a 180-second outer shell guard.

In this environment the runner did not produce a true process hang. The Core project was the long pole and ran for about 2 minutes 10 seconds before completing discovery/execution. The broad run failed inside `Aetheris.Kernel.Core.Tests`, not in InlineStep, because this matched test returned a bounded tessellation failure under solution-level load:

- `Aetheris.Kernel.Core.Tests.Brep.Tessellation.UvTrimMaskExtractorTests.TryExtract_RealBsplineFace_BuildsDeterministicTrimMaskAndSupportsScaffold`

The last especially slow Step242 tests before the failure were bounded display/tessellation/materialization paths, including:

- `Step242TessellationRobustnessTests.Step242_Tessellate_UnsupportedComplexPlanarMultiLoop_DoesNotFallbackToOuterLoopFill` (~17s)
- `Step242Ftc07ViewMaterializationRegressionTests.Step242Ftc07ViewMaterialization_CompletesOrReportsBoundedDiagnostic` (~17s)
- `Step242ImporterTests.Step242_HandcraftedToroidFixture_UsesCircularSeamTopology_AndTessellates` (~29s)

A focused rerun of the failing test passed in about 2 seconds, which isolated the failure to a broad-run wall-clock budget interaction rather than invalid geometry or InlineStep behavior.

## Root cause

`UvTrimMaskExtractorTests.TryExtract_RealBsplineFace_BuildsDeterministicTrimMaskAndSupportsScaffold` is a scaffold extraction/acceptance test, but it obtained its reference mesh through `DisplayPreparationFallbackBuilder.Build(body)`, which uses the production default 5-second bounded tessellation timeout. During a solution-level broad filter, parallel project execution and concurrent long Step242 display/tessellation tests can consume enough scheduler time for the reference tessellation stage to return the documented bounded timeout result instead of a mesh. The test then failed with `Assert.True(fallback.IsSuccess)` before reaching the scaffold assertions.

This is the same family as prior FTC-07 hardening: a production bounded tessellation budget was being used as a test wall-clock assumption for a non-timeout assertion. The observed issue was a slow/bounded-timeout failure under load, not an infinite loop, process-output stall, fixture collision, shared static state mutation, or InlineStep regression.

## Fix/hardening

The test now keeps the existing coverage but separates concerns:

- the scaffold extraction assertions still use the same real imported B-spline face;
- the reference fallback mesh still comes from the real display-preparation path;
- the scaffold-focused assertion uses an explicit 30-second reference-mesh budget so it is not brittle under broad solution-level scheduler load;
- the assertion now prints fallback diagnostics if the bounded reference tessellation still fails.

No Firmament syntax, InlineStep behavior, STEP import/export semantics, or production display behavior changed.

## Why coverage remains intact

Coverage was not weakened because the test still verifies deterministic trim-mask extraction and accepted B-spline UV scaffold construction against a real display reference mesh. Existing display-preparation and FTC-07 regression tests continue to cover the production bounded timeout behavior, including the documented `Viewer.Tessellation.Timeout` diagnostic path. The broad validation filter is unchanged.

## Validation commands

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "FullyQualifiedName~UvTrimMaskExtractorTests.TryExtract_RealBsplineFace_BuildsDeterministicTrimMaskAndSupportsScaffold"
dotnet test Aetheris.slnx -f net10.0 --no-build --filter "INLINE-STEP|InlineStep|PMI|pmi|FirmamentV2|AP242|Build|Step242"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --filter "InlineStep"
dotnet run --project Aetheris.CLI -- --help
git diff --check
```
