# 3DM-BREP-BIND-X1 — complete Product Design reference STEP

**Verdict: Accepted for the recovered reference artifact.** The local Cartesian/Formas Product Design 3DM now binds all 27 source BReps (185 faces, 742 trims), exports 28 closed solids, and reimports one 28-occurrence AP242 assembly. Rhino object 22 contains two disconnected closed components, so it contributes two solids. This is interoperability geometry for inspection and semantic redraw, not Rhino feature-history recovery or an authored Firmament representation.

## Run and artifact

```text
aetheris recover-3dm testdata/3DM/cartesian-product-metres.3dm --bind-step --combined-step --out-dir artifacts/local/3dm-bind-x1/product --json
```

The ignored local assembly is `artifacts/local/3dm-bind-x1/product/recovered-assembly.step` (10.33 MB). The same directory holds deterministic `object-NNN.step` files and `object-022-component-00/01.step`; `artifacts/local/3dm-bind-x1-product.json` is the qualification report. The combined STEP uses the existing AP242 assembly exporter. Its product-structure importer returns 28 solid definitions and 28 identity-transform occurrences. Every imported solid is enclosed and orientation-consistent. Source assets and source-derived STEP/images remain local and uncommitted.

| Result at 0.1 mm | Value |
| --- | ---: |
| Source BReps / fully bound / partial | 27 / 27 / 0 |
| Source and bound faces / trims | 185 / 185; 742 / 742 |
| Exported and reimported solids | 28 / 28 |
| Worst X2 support deviation | 0.09908 mm |
| Worst bound pcurve lift residual | 0.09908 mm |
| Worst per-solid sampled source-to-STEP bounding-box drift | 0.00224445 mm |
| Combined assembly sampled bounding-box coordinate drift | 0.00000862 mm |
| Warm CLI runtime including assembly export/reimport | 16.67 s |

Per-source-object status is explicit in the JSON report: **Qualified** for objects 0–4, 6–20, 22–25; **InspectableRecovered** for 5, 21, 26; **Partial/Unsupported** for none. Every object exports and reimports. `InspectableRecovered` records the singular or near-coincident source-topology qualification described below; it is not an exact-topology claim.

The original support fitting and 0.1 mm budget are unchanged. The separate Rhino vertex tolerances below are *source topology evidence*, not a change to the recovery or kernel tolerance. No unbounded sew/heal operation or mesh geometry authority was introduced.

## Five former partials

| Object | X0 failure | X1 resolution | Measured bound | Final status |
| ---: | --- | --- | --- | --- |
| 1 | Cylinder trim failed strict 0.000001 mm preflight. | Import-only preflight uses the explicit 0.1 mm recovered-geometry consistency budget for edge/support checks; authored export retains the kernel gate. | 0.000012966 mm support residual, below 0.1 mm. | Qualified; STEP reimported. |
| 5 | 48 Rhino singular trim uses have no 3D edge. | Verify each collapsed trim against its source vertex, retain the vertex and ordinary shared edges, and remove only zero-dimensional trim segments from mixed edge loops. The bound BRep retains qualified pcurves; STEP omits those associations on singular mixed loops so its importer can bind the 3D topology. | Maximum source singular lift 0.424267 mm; opposite incident edge endpoints at one Rhino vertex can differ by 0.848533 mm, within Rhino's local 0.864441 mm vertex tolerance. This inherited topology discrepancy exceeds the 0.1 mm *support* budget and is reported separately. | Inspectable recovered; enclosed/oriented STEP reimported. |
| 21 | Two edge endpoints missed their source vertex. | Move source vertex 8 to the coincident incident edge endpoints; retain the 3D curves. Explicit recovered-STEP import policy admits a declared planar inner loop whose sampled crossing stays inside 0.1 mm. Default strict STEP import still rejects this crossing. | Vertex move 0.06000174 mm, allowed source vertex tolerance 0.198568 mm, final endpoint spread 1.7e-13 mm. Inner-loop maximum sampled outside distance 0.00563952 mm. | Inspectable recovered; STEP reimported with explicit bounded import policy. |
| 22 | One shell was disconnected. | Rhino source graph has two independent six-face closed components. Duplicate those source components and bind/export each without stitching. | Two 6-face, 24-trim manifold solids; worst component bbox drift 0.00007149 mm. | Qualified as two solids; both reimported. |
| 26 | Two Rhino singular trims have no 3D edge. | Preserve their collapsed vertex semantics and shared seam edges; omit ambiguous STEP pcurve associations on this mixed singular loop. | Source singular lift below 1.2e-13 mm; component bbox drift 0.00007507 mm. | Inspectable recovered; enclosed/oriented STEP reimported. |

`ThreeDmBoundBody.Adjustments` and the JSON report record each accepted singular collapse or vertex reconciliation with before/after residual and allowed source-local limit. No seam split, arbitrary edge weld, or unrestricted closure repair was made. Bodies without singular topology continue to export qualified pcurves. The X1 STEP artifact is intended for dimensional/visual reference; readers demanding exact pcurve identity for objects 5 and 26 or strict planar containment for object 21 need the retained 3DM provenance and report.

## Visual comparison and semantic handoff

The local `artifacts/local/3dm-bind-x1/lamp-source-step-semantic.png` shows all source 3DM edges, the combined STEP reimport, and the existing semantic Firmament assembly in the same isometric projection and scale. Separate panels are `rhino-source-3dm.png`, `recovered-step-reimport.png`, and `semantic-firmament-lamp.png` in that directory. The source and recovered STEP visibly agree on the base, pole, upper hardware, and shade, including the source's loose cable. The semantic model captures the intended base, pole, joint, head, and shade with simpler upper hardware and no loose cable. No Firmament source was modified.

## Architecture and limits

The source topology bridge remains in `Aetheris.ThreeDm`. Recovered polynomial supports retain `SplineRecoveryProvenance`; source object identity and face/edge index remain available for later replacement with exact supports. The binder uses the shared pcurve recovery and validator. Singular trim and source-vertex tolerance evidence are passed as explicit import-local inputs; default authored STEP preflight and default strict STEP loop classification remain unchanged. The combined assembly is a reference artifact, not the semantic lamp authority.

STEP reimport of object 21 and of the combined assembly requires `ImportPolicy(AllowBoundedNearCoincidentInnerLoop: true, RecoveryToleranceMillimetres: 0.1)`. Its guarded JudgmentEngine candidate requires one declared outer loop, two planar loops, a smaller declared inner area, no disconnected coedge chain, and sampled outside distance within the supplied budget. The default importer still rejects the crossing. Independent third-party CAD opening has not been verified; the local wireframe and Aetheris reimport establish the current visual and topological result.

## Validation

The local Product Design regression asserts 27/27 source objects, 28/28 combined solid roundtrips, unchanged face/trim totals, measured reconciliation, bounded crossing policy, and combined bbox drift. It also retains a deterministic per-body STEP check and the Furniture representative-body regression. The Release solution build passed. The fast kernel lane passed 1,002/1,002 tests, and the full serial solution gate passed, including 452/452 CLI, 1,135/1,135 Core, and 1,692/1,692 Firmament tests. The FrictionLab assembly currently has no discoverable tests.
