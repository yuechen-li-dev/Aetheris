# Getting Started

Preview 3 is qualified for Windows x64. Install the published .NET global tool with the .NET 10 SDK, or run the CLI from a source checkout:

```powershell
dotnet tool install --global Aetheris.CLI --prerelease
aetheris --help
```

The release ZIP is the complete product path: run the commands below from its `Aetheris-win-x64` directory and use `.\aetheris.exe` in place of `aetheris`. It also supplies the `fixtures` used below and Cadmata for the final `view` command. For a source checkout, replace `aetheris` with `dotnet run --project Aetheris.CLI -c Release --`.

## Build a first part

The qualified first part is [`hole-diameter-and-datum.firmament`](../../fixtures/Canonical/PMI/hole-diameter-and-datum.firmament). It is a nontrivial plate with a through hole, datum, and toleranced diameter.

```powershell
aetheris validate fixtures/Canonical/PMI/hole-diameter-and-datum.firmament --json
aetheris build fixtures/Canonical/PMI/hole-diameter-and-datum.firmament --output artifacts/first-part.step --json
aetheris analyze artifacts/first-part.step --json
```

`validate` checks syntax, binding, units, targets, and supported/deferred PMI without materializing geometry. A successful validation exits zero and reports `firmamentV2Validation.status: "valid"`. `build` runs the real geometry and AP242 path; its JSON includes `success`, output path, diagnostics, feature reports, and `pmiExportEvidence` describing what was actually emitted. `analyze` independently reinspects topology and semantic PMI.

Use `aetheris view ...` to open Firmament or STEP in Cadmata when using the Windows bundle.

## Build the canonical ordinary-CAD example

[`machined-mounting-block.firmament`](../../fixtures/Canonical/Integration/machined-mounting-block.firmament) is the first serious example after the small plate. It combines a base, connected Boss, finite Pocket, shaft hole, two counterbores, perimeter EdgeFinish, and semantic PMI while remaining a single readable source file:

```powershell
aetheris build fixtures/Canonical/Integration/machined-mounting-block.firmament --output artifacts/mounting-block.step --json
aetheris analyze artifacts/mounting-block.step --json
aetheris view artifacts/mounting-block.step
```

## Invoke a Template

[`record-array-hole-pattern.firmament`](../../fixtures/Canonical/Patterns/record-array-hole-pattern.firmament) demonstrates typed Records, a static array, a Template, and `Pattern ... Over`:

```powershell
aetheris build fixtures/Canonical/Patterns/record-array-hole-pattern.firmament --output artifacts/pattern.step --json
```

Continue with [geometry](firmament/geometry.md), [materials](firmament/materials.md), [PMI](firmament/pmi.md), [Sheet Metal](firmament/sheet-metal.md), [FEA](firmament/fea.md), [STEP import](firmament/step-import.md), or [Forge interop](forge/interop.md).
