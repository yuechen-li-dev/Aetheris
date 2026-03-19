# Aetheris + Firmament v1

Firmament is a deterministic DSL for generating CAD geometry (STEP AP242) via Aetheris.

## What works today

- primitives and booleans: `box`, `cylinder`, `sphere`, `add`, `subtract`, `intersect`
- placement with `place.on` anchors and `offset[3]`
- validation ops: `expect_exists`, `expect_selectable`, `expect_manifold`
- schema-aware CNC minimum tool radius validation
- deterministic canonical formatting for supported `.firmament` source
- STEP AP242 export for the current single-body golden path

## 30-second demo

Open the example at `testdata/firmament/examples/box_with_hole.firmament`.

Then run the demo-oriented helper test:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "FullyQualifiedName~FirmamentBuildAndExportTests.Run_BoxWithHoleExample_Writes_Default_Export_Artifact"
```

That flow compiles the example, exports STEP using the current last-successful-body policy, and writes the artifact to:

- `testdata/firmament/exports/box_with_hole.step`

For `box_with_hole.firmament`, the exported body is currently the deterministic fallback body selected by the existing export contract, so the demo stays behaviorally accurate without changing runtime semantics.

If you want the API entry point instead of the test wrapper, use `FirmamentBuildAndExport.Run(string sourcePath)` from `Aetheris.Kernel.Firmament`.

## Repo map

- language/compiler/runtime: `Aetheris.Kernel.Firmament/`
- examples: `testdata/firmament/examples/`
- exported STEP artifacts: `testdata/firmament/exports/`

## Docs

- overview: `docs/firmament-overview.md`
- build/export workflow: `docs/firmament-build-workflow.md`
- selector contracts: `docs/firmament-selectors.md`
- demo script: `docs/firmament-demo.md`

## Notes

- This slice keeps the current v1 surface only; it does not add PMI, assemblies, or multi-body export.
- The repository still contains the broader Aetheris server/client scaffolding, but the Firmament kernel path above is the v1 demo-ready entry point.
