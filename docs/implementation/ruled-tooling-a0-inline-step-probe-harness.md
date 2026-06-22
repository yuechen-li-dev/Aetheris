# RULED-TOOLING-A0: InlineStep probe harness for ruled/swept STEP experiments

## Why this exists

RULED-A1 and RULED-A2 proved that Aetheris can preserve exact swept-surface semantics such as:

- `SURFACE_OF_LINEAR_EXTRUSION`
- `SURFACE_OF_REVOLUTION`
- degree-1 `B_SPLINE_SURFACE_WITH_KNOTS` ruled classification

That is enough geometry capability to keep iterating, but it is not enough tooling. Hand-authoring a full AP242 packet every time a developer wants to test one uncertain ruled or swept surface representation is too slow and too error-prone.

RULED-TOOLING-A0 adds a lightweight harness around Firmament V2 `InlineStep` so the developer can focus on the small experimental STEP asset and let Aetheris handle canonical wrapping, real export, reimport, and evidence reporting.

This milestone is tooling only. It does not add new ruled geometry capability, new Firmament ruled syntax, or new AIR ruled transitions.

## Workflow

The harness follows this path:

```text
small probe STEP
  -> aetheris canon --mode production
  -> generated Firmament V2 InlineStep wrapper
  -> aetheris build
  -> canonical AP242 output
  -> aetheris analyze reimport
  -> optional FreeCAD / SolidWorks inspection
```

The important distinction is that the wrapper is generated on demand. Codex does not need to hand-author a full `.firmfixture` just to test one uncertain surface token.

## Entry point

Use:

```powershell
.\tools\Run-RuledStepProbe.ps1 `
  -Probe .\testdata\step242\generated\ruled-a2\ellipse-linear-extrusion-production.step `
  -Name ellipseLinearExtrusionProduction
```

Optional examples:

```powershell
.\tools\Run-RuledStepProbe.ps1 `
  -Probe .\testdata\step242\generated\ruled-a2\ellipse-linear-extrusion-production.step `
  -Name ellipseLinearExtrusionProduction `
  -Open

.\tools\Run-RuledStepProbe.ps1 `
  -Probe .\testdata\step242\probes\surface-of-revolution-line.step `
  -Name surfaceOfRevolutionLine `
  -Out .\demo-output\my-ruled-probes `
  -FreeCAD `
  -Keep
```

## Output layout

The default output directory is:

```text
demo-output/ruled-probes/<probe-name>/
```

Each run writes:

```text
input/
  <original probe filename>
  <probe-name>.canonical-input.step

wrapper/
  <probe-name>.firm

output/
  <probe-name>.canonical.step
  probe-report.json
```

`input/<original probe filename>` preserves the original authored probe filename in the harness output. `input/<probe-name>.canonical-input.step` is the staged canonical STEP that `InlineStep` actually wraps, because current Firmament InlineStep intentionally remains canonical-input-only.

If `-Keep` is supplied, the harness also preserves command artifacts such as canon/build/analyze JSON and optional FreeCAD output for debugging.

## Generated wrapper behavior

The wrapper is intentionally tiny and uses the current Firmament V2 InlineStep syntax:

```firmament
model ellipseLinearExtrusionProductionProbeHarness {
    units mm

    solid ellipseLinearExtrusionProduction: InlineStep {
        path: "../input/ellipseLinearExtrusionProduction.canonical-input.step"
    }
}
```

That means the experimental work stays inside the probe STEP asset. The wrapper is just a deterministic bridge into the real Aetheris pipeline.

## What to inspect

The harness prints a receipt and writes `probe-report.json` with:

- the original probe path;
- the copied probe path;
- the staged canonical InlineStep input path;
- the generated wrapper path;
- the canonical AP242 output path;
- reimport success;
- best-effort analyze status and raw analyzer output when analyze is not currently supported for the emitted body;
- entity evidence from token scans;
- optional FreeCAD status.

The token scan is intentionally simple and easy to read. It reports presence/count for:

- `SURFACE_OF_LINEAR_EXTRUSION`
- `SURFACE_OF_REVOLUTION`
- `B_SPLINE_SURFACE_WITH_KNOTS`
- `ELLIPSE`
- `LINE`
- `CIRCLE`
- `CONICAL_SURFACE`
- `CYLINDRICAL_SURFACE`

This is not a replacement for importer validation; it is a quick receipt for the representation the exporter actually emitted.

For this milestone, the authoritative reimport check is a second `aetheris canon` smoke pass on the emitted output, because that routes through `Step242Importer` and `Step242Exporter` again. `aetheris analyze` is still run as additional evidence, but some open ruled/swept bodies can currently fail there even when canonical reimport succeeds.

## FreeCAD and SolidWorks workflow

`-FreeCAD` is optional. When it is passed, the harness calls `tools/Validate-Step-FreeCAD.ps1` if present.

- If `FreeCADCmd` is available on `PATH`, the harness validates the emitted canonical AP242 output and records the result.
- If FreeCAD is unavailable, the harness reports a clear skip and still succeeds.

SolidWorks remains a manual follow-up step outside CI. The intended path is to open `output/<probe-name>.canonical.step` from the harness output directory.

## Limitations

- Current Firmament `InlineStep` still requires Aetheris-canonical AP242 input, so the harness stages a canonical input STEP before wrapping.
- The harness is intentionally single-probe and single-body oriented.
- The token scan is textual evidence only; it does not claim semantic correctness by itself.
- Some emitted open ruled/swept bodies currently reimport successfully but still fail `aetheris analyze`; the harness reports that separately instead of hiding it.
- FreeCAD is optional and must not be required for normal CI.
- This milestone does not add new ruled/swept geometry capability.
