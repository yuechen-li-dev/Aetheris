# Profile composition inner-loop X1 investigation

Inner loops are explicit material semantics, not inferred decoration.

The STEP exporter serializes the shallow-pocket top cap as one `FACE_OUTER_BOUND` and one `FACE_BOUND`; the latter preserves its clockwise `EDGE_LOOP` traversal. The importer now preserves that declared order and role when exactly one outer bound is present, instead of silently reclassifying and normalizing it from area/winding.

The proven defect was producer-side trim binding. The section-stack emitter assigned `0..2π` to every edge, including lines. During STEP export, vertices without a direct point binding may be recovered from that interval; therefore vertical seam vertices were emitted `2π` units along their line rather than at the declared slab level. The shallow pocket exposed this because its exterior wall is partitioned at the pocket floor. Line bindings now use their exact endpoint distance; circles retain `0..2π`.

After the repair, the shallow-pocket analytic, in-memory, and reimported volumes agree at `3808 mm³` (floating-point result `3807.9999999999995`). The through-cut reimports at `3639.9999999999995 mm³`, matching `3640 mm³`; the raised pad remains `4319.999999999998 mm³` against analytic `4320 mm³`.

Canonical policy: topology edge start/end and `ORIENTED_EDGE.orientation` determine coedge traversal; `EDGE_CURVE.same_sense` only maps curve parameter direction to topology endpoints; `FACE_OUTER_BOUND`/`FACE_BOUND` carry explicit loop role; `FACE_BOUND.orientation` is retained as STEP source evidence; `ADVANCED_FACE.same_sense` applies once between support surface and face orientation. Import must not mutate `EDGE_LOOP` order to satisfy a local winding preference.

`Rect2` is available to the profile-authoring and composition source route with `Center` and `Size`. It derives stable `BottomLeft`, `BottomRight`, `TopRight`, `TopLeft`, `Bottom`, `Right`, `Top`, and `Left` guides. Profiles trace the derived sides through normal Profile IR, with provenance such as `concept:Rect2Layout.Base.Bottom`.

## Hash-tied evidence

The 2026-08-04 artifacts were emitted beneath `artifacts/profile-compose-inner-loop-x1/` and then reimported by `aetheris verify`. Independent BRep mass is enclosed and orientation-consistent in every row. The CLI correctly records external CAD inspection as `ExternalInspectionPending`; that is not substituted for a visual-CAD claim.

| Artifact | SHA-256 | Analytic | Reimported BRep mass |
| --- | --- | ---: | ---: |
| raised pad | `f605df9f5136d51b6a19df46a030684731969928e19f580efb69ee375f5a8924` | 4320 | 4319.999999999998 |
| shallow pocket | `650b43bea4732edafd67c0081fd836967d51e9768b9d1b1c0c37520f80455259` | 3808 | 3807.9999999999995 |
| through cut | `e7c59ede59a3de2546dfef73f9da54cd228d086ed473ae2c0a6a2b3e44bfddf0` | 3640 | 3639.9999999999995 |
| Rect2 profile extrusion | `7dba6137e1c34b008cc238a5558eca6462119928135bfd5f96bc511882382666` | 1000 | build smoke |

The focused regression test `PrismaticProfileCompositionRoundTripTests` asserts the analytic, in-memory mass, STEP export with enforcing preflight, and reimported mass for both the shallow pocket and through cut.
