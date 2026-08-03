# RULED-TOOLING-A0 — InlineStep probe harness

## Purpose

`tools/Run-RuledStepProbe.ps1` is a tooling only harness for ruled and swept STEP experiments. It does not add Firmament syntax, geometry support, AIR nodes, or production routing.

The harness lets a small STEP probe travel through a real Aetheris round trip:

```text
probe STEP
  -> aetheris canon --mode production
  -> generated Firmament V2 InlineStep wrapper
  -> aetheris build
  -> canonical AP242 STEP
  -> canonical reimport smoke
  -> aetheris analyze
  -> probe-report.json
```

## Usage

```powershell
.\tools\Run-RuledStepProbe.ps1 `
  -Probe .\testdata\step242\generated\ruled-a2\ellipse-linear-extrusion-production.step `
  -Name ellipseLinearExtrusionProduction
```

Optional parameters:

- `-Out <directory>` selects an output root.
- `-FreeCAD` invokes `tools/Validate-Step-FreeCAD.ps1` after Aetheris validation.
- `-Keep` retains the intermediate reimport STEP.
- `-Open` opens the output directory after completion.

The default output directory is:

```text
demo-output/ruled-probes/<probe-name>/
```

The layout is:

```text
input/
  <original probe>
  <probe-name>.canonical-input.step
wrapper/
  <probe-name>.firm
output/
  <probe-name>.canonical.step
  probe-report.json
```

`probe-report.json` records the real output path, reimport/analyze status, topology summary, selected swept/ruled STEP marker counts, and optional FreeCAD evidence.

## Interpretation

Passing the harness proves that the supplied representation survives the current canonical STEP, InlineStep, export, reimport, and analyzer paths. It does not prove that Aetheris can author the same surface from a general Construction AIR node, nor that every degree-1 spline is a valid chamfer surface.
