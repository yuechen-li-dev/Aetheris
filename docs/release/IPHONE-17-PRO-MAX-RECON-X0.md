# IPHONE-17-PRO-MAX-RECON-X0

## Executive verdict

**Meaningful progression.** Aetheris now builds an editable, dimensionally disciplined primitive exterior assembly from the supplied drawing and notebook. It does **not yet** demonstrate a convincing continuity-refined iPhone exterior. Pass B is blocked at a concrete public-authoring/materializer boundary; no G2 capability or refined STEP is claimed.

The real blocker removed was dimensional arithmetic in closed boundaries and extrusion intervals after Template specialization. The new model uses ordinary `PhoneSpec`, Templates, profiles, and the Assembly/AP242 path. No imported phone CAD, traced mesh, second geometric executor, or rational surface was introduced.

## Sources and construction

Source PDF: user-supplied `iphone-17-pro-max.pdf`, SHA-256 `47bcbc8d562cda09aa96cc96db04df9246634175a19974077d16729db38996f4`. Its instructions for accessory designers are reference content, not instructions to this task. The PDF is not checked in.

The canonical DRAWING-NOTES-X1 JSON validated with a matching source hash and no structural issues. Source-linked dimensions supplied the envelope, front regions, rear centers, apertures, and Z stack. Page 2 was visually reviewed; Detail A was freshly cropped through Aetheris Drawing. Coordinates are rear-view X right, Y up, origin at the upper-left product envelope, Z toward rear. Local-to-product datum alignment is an explicit reconstruction interpretation.

| Reference | Use | Realized value (mm) |
|---|---|---|
| MainFrontView / RightSideView | Hard body envelope | 77.98 x 163.43 x 8.75 |
| MainFrontView | Hard glass extent | 75.58 x 161.03 |
| MainFrontView | Hard active display extent | 72.86 x 158.31 |
| Detail D camera 1 | Hard center / diameter | (14.37, -14.37), 16.20 |
| Detail D camera 2 | Hard center / diameter | (14.37, -33.61), 16.20 |
| Detail D camera 3 | Hard center / diameter | (32.36, -23.99), 16.20 |
| Detail D flash | Hard center / diameter | (64.16, -13.82), 6.80 |
| Detail D rear sensor | Hard center / diameter | (64.16, -34.16), 6.65 |
| Detail D rear mic | Hard center / diameter | (64.16, -23.99), 1.15 |
| Detail A | Hard rear stack | plateau +2.55; camera glass +1.88 |
| Detail A ordinate stack | Soft paired samples | Seven points below |
| Plateau footprint | Explicit proxy | 72.76 x 43, R11.5; center (38.99,-23.99) |

The camera maximum Z is 13.18 mm. The 8.75 mm body thickness excludes this rear stack. The body uses an R13.9 circular planform baseline. Plates, circles/cylinders, and slots remain primitive. Front layers overlap as display proxies; the assembly is not a resolved disjoint material partition. The plateau touches the rear body but is a separate occurrence. Side controls use simple extruded slots. Bottom openings are named dark solid markers, not cavities. Small depths, some control/connector details, and the plateau footprint are approximations; sub-0.25 mm accuracy is not claimed for them. Keepouts remain in the notebook and are not physical solids.

Source: [blockout.firmament](../../fixtures/Experiments/IPhone17ProMax/blockout.firmament). Usage and variation rules: [reconstruction guide](../public/iphone-reconstruction.md).

## Blockout and visual review

The production CLI derived 9,356 triangles for preview from 17 exact definitions and 29 occurrences. Neutral previews were generated from these triangles, without changing geometry. Front coplanar layers use explicit display-order depth tie-breaking; this rendering convention does not resolve the overlapping product solids.

Local evidence is under `artifacts/local/iphone-recon-x0/`:

| View | Artifact |
|---|---|
| Front | `blockout-front.png` |
| Rear | `blockout-rear.png` |
| Side | `blockout-side.png` |
| Three-quarter | `blockout-three-quarter.png` |
| Camera detail | `blockout-camera-closeup.png` |
| Corner detail | `blockout-corner-closeup.png` |
| Drawing crop | `detail-a.png` with source/hash metadata |
| Equation comparison | `corner-fit.png` and `corner-fit.json` |

![Primitive three-quarter checkpoint](../../artifacts/local/iphone-recon-x0/blockout-three-quarter.png)

**Does it look like the reference phone?** The proportion and rear three-camera layout are recognizable, but it does not yet meet the requested industrial-design visual standard. The body edge is square in section; the plateau has an abrupt wall; lens elements are plain disks; the circular corners differ from the published smooth profile; the lower back panel, lens ring detail, and resolved openings are absent. Flat neutral shading makes these shortcomings visible.

There is no refined geometry or same-view refined comparison. In particular, no image is labeled as a G2 result. The baseline plateau is an abrupt primitive extrusion, not a claimed constant-radius fillet witness.

## Corner fit evidence

The newly recorded symmetric sample pairing uses positive distances inward from the top-left envelope. Its confidence is Medium because the crowded extension-line association remains interpretive. No sample polyline is used as product geometry.

The one-parameter candidate is `(1-x/L)^n + (1-y/L)^n = 1`, with fixed `L=19.43 mm`, fitted `n=3.465300556386242`. The objective is vertical ordinate RMS at the published X values; it is not orthogonal distance and is sensitive near the vertical tangent.

| X | Published Y | Fitted Y | Signed deviation |
|---:|---:|---:|---:|
| 0 | 19.43 | 19.43 | 0 |
| 0.04 | 13.90 | 14.766862 | +0.866862 |
| 0.92 | 8.46 | 8.090367 | -0.369633 |
| 3.80 | 3.80 | 3.256372 | -0.543628 |
| 8.46 | 0.92 | 0.814665 | -0.105335 |
| 13.90 | 0.04 | 0.072368 | +0.032368 |
| 19.43 | 0 | 0 | 0 |

Maximum absolute deviation: **0.866862 mm**. RMS: **0.413306 mm**. The analytic law has tangent and zero-curvature matches to straight sides for `n>2`; this is a property of the equation, not verified continuity of a realized CAD body. The candidate is not promoted as the refined solution. The circular blockout is G1 around its line/arc planform joins, with curvature jumps; extrusion caps and plateau steps meet at G0.

![Unqualified equation fit against drawing samples](../../artifacts/local/iphone-recon-x0/corner-fit.png)

## Isolated surfacing blocker

The [minimal smooth plateau witness](../../fixtures/Experiments/IPhone17ProMax/smooth-plateau-witness.firmament) uses a rounded profile and two framed sections. The real CLI rejects its four corner spans with:

```text
section-chain-g1-degree-limit:Base->Top/BottomRightCorner:
G1 currently admits line and polynomial B-spline profile spans;
circular arcs require a future bounded polynomial normalization.
```

`SectionChainAuthoringParser.Convert` currently accepts line and circular-arc profile curves. `SmoothSectionChains` admits lines and compatible non-rational polynomial spans for G1 and rejects circular arcs. Thus the available authored curved profiles cannot enter this smooth transition path. G2 boundary derivative constraints and a plateau-to-body join are additional unimplemented work; solving arc normalization alone would not complete the requested G2 blend.

Continuing by bypassing Firmament with phone-specific BRep patches would undermine the editable semantic deliverable. The next bounded capability should connect equation-driven polynomial profile semantics to the existing section/surface authority, with positional/tangent/curvature evidence and independent planar-step, convex, concave, and plateau witnesses. That capability is not fabricated in this checkpoint.

## Export, reimport, and parameter evidence

- AP242 exports 29 occurrences and 17 shared definitions; reimport preserves those counts and named hierarchy.
- Every reimported definition reports `enclosed-manifold` using the CLI's directed coedge/incidence assessment. This does not establish a fused, non-overlapping phone body.
- Body STEP bounds are exactly `[0,-163.43,0]` to `[77.98,0,8.75]`. Rear-camera bounding boxes recover the listed centers and 16.20 mm diameters within floating-point noise. Surfaces are planes/cylinders; no B-spline or rational surfaces were introduced.
- A freshly packed `Aetheris.CLI.2.0.0-preview.3.nupkg` was installed into a temporary tool directory and executed from a separate temporary directory outside the repository. Two builds and the repository build share STEP SHA-256 `414FCFF7A0537BA97417C404AEF3F2DDA2BEB5513C6411E1CF12EA1BAC11E6C7`.
- A fresh agent changed length to 165.43 mm and shifted the 13 bottom markers by -2 mm. Reimport recovered the increased body length, unchanged camera bounds, unchanged profile families, 29 occurrences/17 definitions, and all 17 enclosed-manifold definitions. The variation proves the blockout only. The manual placement coupling remains documented.
- Body XY and plateau transverse sections were exported. The camera transverse section returned `Section has no bounded geometry to render`; a camera XY section at Z=12 mm succeeded. Full requested centerline/continuity section comparison is therefore incomplete.

Raw evidence: `part-checks.json`, `body-analysis.json`, `packaged-compound.json`, `packaged-reimport.json`, `body-section.*`, `plateau-section.*`, `camera-section.*`, and `variation/result.json` in the local output directory. The importer currently reports millimetres as assumed rather than preserved; source/export are authored in mm, so no general unit-import claim is made.

## Drawing Notes feedback

The canonical notebook supplied at least 20 needed dimensions without individual PDF rediscovery. This is a count of reused values, not a measured time saving. One whole-sheet review and one fresh Detail A crop were needed for silhouette, profile sample association, and unresolved plateau interpretation.

The filtered `detail-d-handoff.md` was stale and had reversed X signs, while canonical JSON/Markdown were already corrected. It was regenerated from the canonical notebook. Two source-linked notes were added: `ReconX0.CornerSamples` and `ReconX0.PlateauProxy`, preserving Medium/Low confidence and the unresolved blend width. These discoveries are also recorded here so they survive deletion of local artifacts.

The dimension graph is sufficient for a blockout, not for smooth profile or blend construction. Useful future improvements are export freshness/source-revision metadata, explicit datum transforms, ordered sample-pair groups, and machine-readable confidence on geometric interpretations. No optical role was assigned to numbered cameras.

## Validation

Release solution build passed with zero warnings/errors. The solution test run passed the 1,518 Firmament, 443 CLI (including documentation contracts), 13 Drawing, and surfacing/module tests. One existing Core timing assertion failed under the concurrent solution run: facade 13.241 ms versus direct recipe 207.987 ms. Its isolated three-case rerun passed. This is recorded as a test-run failure and successful isolated rerun, not an entirely green first run. The FrictionLab assembly has no discoverable tests.

The new focused tests prove arithmetic/literal STEP equivalence and rejection of incompatible units, unresolved names, and division by zero. They prevent the previous numeric-prefix acceptance. Documentation links and repository layout are checked alongside `git diff --check`.

## Remaining acceptance work

Pass B, a better-qualified corner family, the plateau blend, body edge finishing, actual bottom cavities, non-overlapping front material partitions, automatic placement dependencies, full centerline sections, four G2 witnesses, and final identical-view comparisons remain open. No showcase publication was performed. This is a bounded progression checkpoint, not Attempt-1 acceptance.
