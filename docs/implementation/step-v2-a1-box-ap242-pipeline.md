# STEP-V2-A1 — Firmament V2 Box AP242 pipeline

STEP-V2-A1 connects the smallest Firmament V2 production path to the existing AP242 back half for **Box only**.

## Pipeline path

The implemented path is:

```text
Firmament V2 Box source
  -> FirmamentV2Parser
  -> FirmamentV2BuildLowering
  -> FirmamentLoweredBoxParameters
  -> FirmamentPrimitiveExecutor
  -> BrepBody
  -> Step242Exporter
  -> emitted AP242
  -> Step242Importer / StepAnalyzer verification
```

`FirmamentV2BuildLowering` is intentionally narrow. It accepts exactly one V2 `Box` solid with no `modify` blocks and lowers only that solid into the existing lowered primitive representation. It does not introduce a V2-only BRep builder or exporter.

## Fixture

The Tier 0 proof fixture is:

```text
fixtures/FirmamentV2/Primitive/valid/pipeline-v2-box-step-verified.valid.firmfixture
```

It declares:

- `fixture-id: pipeline-v2-box-step-verified`
- `expected-stage: step-verified`
- `build-command: aetheris build`
- expected topology: 6 faces, 8 vertices, 12 edges
- expected volume: 480 mm^3 for size `[10, 8, 6]`

## Command under test

The integration test invokes the real production CLI path:

```bash
aetheris build fixtures/FirmamentV2/Primitive/valid/pipeline-v2-box-step-verified.valid.firmfixture --out <temp>/pipeline-v2-box-step-verified.step --json
```

This reaches `FirmamentBuildAndExport.Run`, uses the V2 Box lowering bridge, executes the existing primitive executor to produce a real `BrepBody`, and calls the real `Step242Exporter`.

## Verification checks

The integration proof in `Aetheris.CLI.Tests/FirmamentV2BoxStepPipelineTests.cs` checks that:

1. the real build command accepts the V2 Box fixture;
2. the emitted STEP file exists;
3. the AP242 text contains at least 6 `ADVANCED_FACE` entities;
4. the AP242 text contains `VERTEX_POINT` entities;
5. the output is not a trace-only artifact;
6. `Step242Importer.ImportBody` reimports the emitted AP242;
7. reimported topology is exactly 6 faces, 8 vertices, and 12 edges;
8. `StepAnalyzer.AnalyzeVolume` reports exact volume 480 within tolerance.

The fixture trace path also performs these checks before reporting `step-verified`, so the stage label is not parser-only or trace-only.

## Deferred scope

STEP-V2-A1 intentionally does **not** wire Cylinder, Cone, Sphere, Torus, prism families, slots, rounded boxes, chamfers, fillets, drafts, library parts, side-hole exporter rerouting, patterns, PMI, or DFM enforcement. Those remain outside this Box-only Tier 0 gate.

## MVP contract relationship

This milestone is the first implementation slice for `docs/mvp/firmament-v2-ap242-mvp-readiness-contract.md`: a Firmament V2 fixture is considered ready only when real AP242 is emitted from a real `BrepBody` by `Step242Exporter` and independently verified by reimport, topology checks, and volume checks.

## Forward link

STEP-V2-X1 extends this Box-only bridge to Cylinder, Cone/frustum, Sphere, and Torus while preserving the V2-to-lowered compatibility seam; see `docs/implementation/step-v2-x1-analytic-primitives-ap242.md`.
