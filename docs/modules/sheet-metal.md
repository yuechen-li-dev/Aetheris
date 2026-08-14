# Sheet Metal Module

`Aetheris.SheetMetal` 0.2.0 is the first implemented Sheet Metal milestone. It owns manufacturing semantics above Core BRep: nominal thickness, reference-sheet regions, explicit bends, neutral-axis policy, cut correspondence, flat-pattern lowering, validation, and bounded DFM. Imported STEP remains the formed-geometry authority; recovered `SheetMetalPartIr` is an evidence-bearing interpretation and never silently replaces or repairs the source.

## Supported M1 boundary

- Firmament V2 module-owned `SheetMetal` syntax for one rectangular base with two opposite 90° flanges and circular base holes.
- Exact formed analytic BRep for that authored family: planar skins, concentric cylindrical bends, exact circular hole walls, closed-manifold STEP AP242 export, and no generic Boolean fallback.
- Tolerance-bounded recognition of parallel planar skin pairs and coaxial cylindrical skin pairs in ordinary exact BRep/STEP.
- Planar midpoint reference planes and geometric mid-cylinders. These are deliberately distinct from the manufacturing neutral axis.
- Planar/cylindrical developability using the existing Surfacing evidence vocabulary.
- Deterministic largest-planar-area base selection, bend-graph traversal, exact neutral-axis cylindrical development, cut mapping, overlap checks, a thickness-bearing flat AP242 solid, explicit recovered Firmament intent, and a compact secondary SVG view.
- Explicit provisional K-factor policy. The default is `0.5`; authored source may override it. This is not a material/process database.
- Parameterized first-pass DFM checks for positive thickness, bend-radius ratio, hole-to-bend distance, and flat overlap.

M1 does not advertise arbitrary forming, stamping, hems, jogs, beads, springback, arbitrary lofted bends, double-curvature approximation, bend tables, automatic relief families, or a production nesting boundary. Unsupported and ambiguous input returns typed `Partial`/`Unsupported` evidence.

## Canonical commands

```text
aetheris sheetmetal inspect part.step
aetheris sheetmetal flatten part.step --step part-flat.step --firmament part-recovered.firmament --svg part-flat.svg --k-factor 0.5
aetheris build fixtures/FirmamentV2/SheetMetal/simple-u-channel.firmament
```

The canonical authored fixture is [`simple-u-channel.firmament`](../../fixtures/FirmamentV2/SheetMetal/simple-u-channel.firmament). The hostile imported fixture is the existing NIST CTC-03 corpus file at `testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp`.

## Recognition doctrine

Thickness candidates come from overlapping parallel planes and axially overlapping coaxial cylinders. Candidate separations are clustered under an explicit linear tolerance, then the bounded alternatives are admitted/scored/tie-broken by `JudgmentEngine`. BRep face IDs remain source bindings only; stable semantic region and bend IDs derive deterministically from ordered source bindings.

The flat STEP is generated from the same analytic flat IR as the Firmament and SVG artifacts. It is a closed thickness-bearing solid and re-imports through Aetheris, but the current imported flattening preserves recovered region hulls and mapped inner-loop cut profiles rather than stitching every source edge fragment into an exact production-ready blank contour. That boundary-stitching/corner-relief problem is why imported CTC-03 remains `Partial`.

See the compact [M1 evidence bundle](sheetmetal/artifacts/m1/README.md).
