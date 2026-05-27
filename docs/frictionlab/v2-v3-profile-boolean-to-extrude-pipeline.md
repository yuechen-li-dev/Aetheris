# V2-V3 — Profile Boolean expression to ProfileHoleExtrude chained pipeline

## Purpose and scope
Lab-only chained evaluation proving: `ProfileBooleanExpr2D -> ResolvedProfile2D -> ProfileHoleExtrudeEmitter -> BRep/STEP` for the bounded V2-X4 subset, without any 3D Boolean operation.

## References
- `docs/aetheris-v2-sweep-first-architecture.md`
- `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- `docs/frictionlab/v2-x1-resolved-profile2d-lab.md`
- `docs/frictionlab/v2-x3-profile-with-hole-extrude-lab.md`
- `docs/frictionlab/v2-x4-profile-boolean-normalization-lab.md`
- `docs/v2-v1-profile-hole-extrude-production-evaluation.md`
- `docs/v2-v2-profile-hole-extrude-through-hole-integration.md`

## Supported subset
- `Difference(Rectangle, CircleInside...)` (single or multiple circles).
- Deferred/rejected behavior preserved from V2-X4 for unsupported/deferred shapes.

## Candidate and adapter
`ProfileBooleanExtrudePipelineLab` normalizes via `ProfileBooleanNormalizationLab.Normalize`, then adapts `LabResolvedProfile2D` loops to `ProfileHoleExtrudeRequest` (rectangle width/depth + circular hole loops) and emits through `ProfileHoleExtrudeEmitter`.

## Findings
- Success: centered, off-center, and two-hole rectangle-minus-circle cases emit STEP-smoke-valid BRep.
- Invalid and deferred normalization cases stop before emission with deterministic diagnostics.
- Topology in success cases: planar faces = 6, cylindrical faces = hole count.
- STEP smoke markers present: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`, `CYLINDRICAL_SURFACE`; `BREP_WITH_VOIDS` absent.
- No-3D-Boolean diagnostics emitted (`v2-x5-no-3d-boolean-used`, compatibility `v2-v3-no-3d-boolean-used`).

## Non-goals reaffirmed
No production routing expansion, no full clipping engine, no sketch solver, no blind/counterbore/stepped/cross-axis hole support, no STEP exporter change, and no Boolean core change.

## Production-evaluation posture
Current result is lab-only evidence and is ready for bounded production-evaluation discussion for profile-expression front door routing.

## Recommended next step
Productionize bounded profile-expression front door (rectangle-minus-contained-circles), then run slot/capsule lab and/or bounded clipping lab based on deferred evidence.
