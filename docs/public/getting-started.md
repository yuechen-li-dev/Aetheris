# Getting Started

Start with a self-contained [bundle](release-bundle.md) for Windows x64, Linux x64 or macOS Apple Silicon. It includes the compiler, Cadmata viewer, examples and runtime. If you only need the CLI, install the .NET global tool with the .NET 10 SDK:

```powershell
dotnet tool install --global Aetheris.CLI --version 2.0.0
aetheris --help
```

In a bundle, run the commands below from the extracted directory and use `.\aetheris.exe` (Windows) or `./aetheris` (Linux/macOS) in place of `aetheris`. The bundle supplies the `fixtures` used below and Cadmata for the final `view` command. For a source checkout, replace `aetheris` with `dotnet run --project Aetheris.CLI -c Release --`.

## Build a first part

The qualified first part is [`hole-diameter-and-datum.firmament`](../../fixtures/Canonical/PMI/hole-diameter-and-datum.firmament). It is a nontrivial plate with a through hole, datum, and toleranced diameter.

```powershell
aetheris validate fixtures/Canonical/PMI/hole-diameter-and-datum.firmament --json
aetheris build fixtures/Canonical/PMI/hole-diameter-and-datum.firmament --output artifacts/local/first-part.step --json
aetheris analyze artifacts/local/first-part.step --json
```

Normal `validate` checks syntax, binding, units, targets and capability admission, including the engineering preflight required by the domain. It can construct geometry in memory; it does not write a build artifact. A successful validation exits zero and reports `firmamentV2Validation.status: "valid"` with its validation level. `--parse-only` is a limited check and does not prove build admission. `build` writes the real geometry and AP242 artifact; its JSON includes `success`, output path, diagnostics, feature reports, and `pmiExportEvidence` describing what was actually emitted. `analyze` independently reinspects topology and semantic PMI.

Use `aetheris view ...` to open Firmament or STEP in Cadmata when using a bundle.

## Build the canonical ordinary-CAD example

[`machined-mounting-block.firmament`](../../fixtures/Canonical/Integration/machined-mounting-block.firmament) is the first serious example after the small plate. It combines a base, connected Boss, finite Pocket, shaft hole, two counterbores, perimeter EdgeFinish, and semantic PMI while remaining a single readable source file:

```powershell
aetheris build fixtures/Canonical/Integration/machined-mounting-block.firmament --output artifacts/mounting-block.step --json
aetheris analyze artifacts/mounting-block.step --json
aetheris view artifacts/mounting-block.step
```

## Invoke a Template

[`record-array-hole-pattern.firmament`](../../fixtures/Canonical/Patterns/record-array-hole-pattern.firmament) demonstrates typed Records, a static array, a Template, and `Pattern ... Over`:

When the elements need stable semantic names, use [`mounting-points.firmament`](../../fixtures/Canonical/Set/mounting-points.firmament): `Set<Point2>` preserves authored names and order, `MountPoints.UpperRight` addresses one value, and Pattern maps the complete finite set into construction.

```powershell
aetheris build fixtures/Canonical/Patterns/record-array-hole-pattern.firmament --output artifacts/pattern.step --json
```

## Next

Work through [Learn Firmament](learn/README.md) to write parts like these yourself, or browse the [guides](README.md#guides) for sheet metal, FEA, STEP import and the rest.
