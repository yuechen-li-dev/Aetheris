# Getting Started

Preview 3 is qualified for Windows x64. Install the published .NET global tool with the .NET 10 SDK, or run the CLI from a source checkout:

```powershell
dotnet tool install --global Aetheris.CLI --prerelease
aetheris --help
```

For a source checkout, replace `aetheris` below with `dotnet run --project Aetheris.CLI -c Release --`.

## Build a first part

The qualified first part is [`box-hole-pmi.firmament`](../../fixtures/FirmamentV2/Canonical/valid/box-hole-pmi.firmament). It is a nontrivial plate with a through hole, datum, and toleranced diameter.

```powershell
aetheris validate fixtures/FirmamentV2/Canonical/valid/box-hole-pmi.firmament --json
aetheris build fixtures/FirmamentV2/Canonical/valid/box-hole-pmi.firmament --output artifacts/first-part.step --json
aetheris analyze artifacts/first-part.step --json
```

`validate` checks syntax, binding, units, targets, and supported/deferred PMI without materializing geometry. `build` runs the real geometry and AP242 path. In JSON, `success: true`, the output path, diagnostics, feature reports, and `pmiExportEvidence` describe what was actually emitted. `analyze` independently reinspects topology and semantic PMI.

Use `aetheris view ...` to open Firmament or STEP in Cadmata when using the Windows bundle.

## Invoke a Template

[`record-array-pattern-holes.firmament`](../../fixtures/FirmamentV2/Canonical/valid/record-array-pattern-holes.firmament) demonstrates typed Records, a static array, a Template, and `Pattern ... Over`:

```powershell
aetheris build fixtures/FirmamentV2/Canonical/valid/record-array-pattern-holes.firmament --output artifacts/pattern.step --json
```

Continue with [geometry](firmament/geometry.md), [materials](firmament/materials.md), [PMI](firmament/pmi.md), [Sheet Metal](firmament/sheet-metal.md), [FEA](firmament/fea.md), [STEP import](firmament/step-import.md), or [Forge interop](forge/interop.md).
