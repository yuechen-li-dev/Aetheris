# SIDEHOLE-REAL-X1 — real exporter reroute for locked Firmament V2 side-hole

## Audit finding

The current CLI-visible side-hole artifact path was `aetheris trace --fixture ... --out-dir ...`. That path wrote `side-hole*.step` from `SideHoleGoldenPathArtifacts.StepText` in `Aetheris.CLI/CliRunner.cs`, a hardcoded AP242-shaped string containing comments such as `controlled fixture only`, `region-parent-integrated`, and `generated-on-demand fixture artifact`. It was trace evidence, not a real `BrepBody` exported by `Step242Exporter`.

The parser/lowering path for the locked V2 fixture already existed at:

```text
fixtures/Region/valid/feature-v2-side-hole-step-verified.valid.firmfixture
```

The fixture parses as a box-like host (`Box size [10, 8, 6]`) with a controlled `region sideHole on face(+X)` and `through: face(-X)`, radius `1`, centered in the face-local frame. The route policy still admits only the existing locked +X/-X side-hole path; this milestone did not broaden side-hole admissibility.

## New real build/export path

The production build command now recognizes only the locked Firmament V2 +X/-X side-hole region and materializes it narrowly as:

```text
Firmament V2 source fixture
  -> FirmamentV2Parser / DFM validation / existing side-hole route policy
  -> controlled box-minus-cylinder BrepBody
  -> Step242Exporter.ExportBody
  -> AP242 file on disk
  -> Step242Importer reimport in tests/analyzer
  -> topology/evidence + exact volume verification
```

Implementation note: the existing exact box-minus-Z-cylinder BRep builder is reused by building the canonical side-hole as a Z-through hole in a permuted coordinate frame, then reorienting the BRep geometry into the real X-axis side-hole frame. This is a narrow locked-route bridge, not arbitrary-axis side-hole support.

## Fixture and command path

Fixture:

```text
fixtures/Region/valid/feature-v2-side-hole-step-verified.valid.firmfixture
```

CLI build path:

```bash
dotnet run --project Aetheris.CLI -f net10.0 -- build fixtures/Region/valid/feature-v2-side-hole-step-verified.valid.firmfixture --out "$tmp/feature-v2-side-hole.step" --json
```

Volume analysis path:

```bash
dotnet run --project Aetheris.CLI -f net10.0 -- analyze volume "$tmp/feature-v2-side-hole.step" --json
```

## AP242 verification checks

The integration test asserts:

- the CLI `build` command succeeds and writes AP242;
- emitted STEP contains `ADVANCED_FACE`, `VERTEX_POINT`, and `CYLINDRICAL_SURFACE`;
- emitted STEP does not contain the old trace/hardcoded sentinel text (`trace`, `controlled fixture only`, or `golden path artifact`);
- `Step242Importer.ImportBody` reimports the file;
- imported topology has faces and vertices;
- imported topology has exactly one cylindrical side-wall face with an X-axis cylinder;
- exact volume analysis reports `analytic-box-minus-x-hole`.

## Volume formula

For the locked fixture dimensions:

```text
base box = 10 * 8 * 6
hole radius = 1
through depth along X = 10
expected = 10 * 8 * 6 - π * 1² * 10
```

The expected volume is computed independently in the test and compared to exact analyzer output with the existing `1e-8` tolerance convention used by related STEP V2 volume tests.

## Deferred / non-scope

Deferred intentionally:

- arbitrary-axis side holes;
- side holes on every face;
- side-hole patterns or variants;
- counterbore/countersink/threaded side holes;
- general region Boolean composition;
- chamfer/fillet/draft/PMI/DFM changes.

The trace artifact writer remains trace-only legacy evidence for `aetheris trace --out-dir`; it is no longer the MVP readiness proof for the locked Firmament V2 side-hole fixture. MVP readiness for this fixture is tied to the real `aetheris build` path and the AP242 `step-verified` contract: parsed, semantic-lowered, air-materialized, brep-built, step-emitted, step-roundtrip, and step-verified.
