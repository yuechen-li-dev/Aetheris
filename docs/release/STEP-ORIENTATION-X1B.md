# STEP-ORIENTATION-X1B — Periodic and Degenerate Boundary Winding Repair

## Executive verdict

**Meaningful progression.** The four named X1 round-trip regressions now preserve every canonical face orientation across STEP export/re-import without restoring source `same_sense` authority. The complete 17-file NIST round-trip corpus passes. The milestone is not marked Accepted because STEP pcurve ingestion and explicit pole/apex `VERTEX_LOOP` preservation remain incomplete; claiming general seam-side and degenerate-boundary stability would exceed the evidence.

## Authority and policy

X1 remains unchanged:

- `ADVANCED_FACE.same_sense` is provenance and mismatch evidence only.
- `ResolvedFaceOrientation` is Aetheris authority.
- downstream geometry consumes the resolved value once.

JudgmentEngine was evaluated for the requested “multiple input conditions, one correct answer” policy. It is used at the STEP face-boundary export boundary:

1. complete explicit `FaceBoundaryRoleBinding` evidence is admissible and wins;
2. authored loop order is admissible only when no explicit role evidence exists;
3. partial, cross-face, or multiply-outer role evidence has no admissible candidate and export stops.

JudgmentEngine is not used for UV arithmetic. Period equivalence, unwrapping, and winding remain deterministic geometry rather than scored preference.

## Root causes and repairs

### Lost face-bound role

The exporter sorted `Face.LoopIds` and labeled the first result `FACE_OUTER_BOUND`. Imported loop IDs are allocation identity, not boundary role. On multi-loop faces this promoted a hole to the exterior while preserving every edge use, producing severe volume corruption.

`FaceBoundaryRoleBinding` now preserves outer/inner role per face/loop. Import records it, binding validation checks ownership, the orientation resolver preserves it, and the JudgmentEngine-backed exporter serializes the explicit outer bound first without changing loop identity or traversal.

### Full-period zero-area boundaries

FTC-11 cylinder faces contain one-edge full circles. Their continuous UV lift spans `+/-2pi` at constant axial coordinate, so ordinary shoelace area is exactly zero. The former fallback averaged the 3D circle samples to the cylinder axis and attempted to derive a radial support normal there. Tiny serialization noise selected different radial directions before and after round-trip.

`SurfacePeriodicity` now describes U/V periods for analytic and closed B-spline supports and provides `EquivalentModuloPeriod`. A full-period constant-secondary-coordinate boundary uses its signed unwrapped traversal directly. It is never classified from a singular averaged support normal.

### Sphere and cone seam sensitivity

STC-10 and FTC-08 first diverged on spherical loops whose edge topology and support frame were unchanged. Tiny direction components rounded through STEP serialization changed the chosen UV seam lift and reversed shoelace sign. CTC-02 showed the same class at conical apex/seam loops.

Sphere loops now prefer support-relative 3D boundary-normal evidence. Cone loops use signed full-period traversal when applicable and otherwise prefer support-relative boundary-normal evidence before seam-sensitive UV area. These choices are derived from edge-use traversal and support geometry, not file face flags.

## Original failures

| Case | Prior X1 status | First isolated cause | X1B final status |
| --- | --- | --- | --- |
| STC-10 | volume approximately `197357 -> -52028.4` | lost multi-loop role, then seam-sensitive sphere UV area | every face orientation preserved; `202864.773478 -> 202842.039837` |
| FTC-08 | negative/near-zero round-trip volume variants | lost multi-loop role, then seam-sensitive sphere UV area | every face orientation preserved; `505306.986523 -> 505504.056584` |
| FTC-11 | volume approximately `3989.48 -> -4389.53` | full-period cylinder loop had zero UV area and singular centroid-normal fallback | every face orientation preserved; `6861.467338 -> 6861.515022` |
| CTC-02 | enclosed-volume gate failed/open topology | conical seam/apex UV lift changed across serialization | every local face orientation preserved; remains open/global-unknown; `0 -> 0` signed closed-volume claim |
| STC-06 | ambiguous | contradictory/insufficient boundary evidence | remains explicitly `Ambiguous`; not promoted |

The changed absolute starting volumes reflect the corrected X1B derivation on the initial import, not a post-import mass repair. Mass properties continue to expose the resolved topology and do not flip faces.

## Representative traces

FTC-11 face 3 is a cylinder with two single-edge full-circle loops. Before repair, loop 7 changed support baseline `True -> False` even though the support, edge use, vertices, `ORIENTED_EDGE`, circle, and trim were equivalent. The UV trace was a constant-V line with signed U span of one period. X1B classifies that edge-use trace by its signed period traversal, so both imports choose the same baseline.

STC-10 face 193 is a spherical three-edge loop. Its support and topology were byte-equivalent at semantic precision; a source circle normal component of approximately `3.47e-15` serialized as zero, selecting the other UV seam lift. Support-relative 3D boundary evidence is invariant to that harmless rounding and now yields the same orientation.

Imported bodies currently contain zero retained pcurve bindings for these corpus witnesses. This is why the report does not claim pcurve/seam-side acceptance: `SURFACE_CURVE`/`SEAM_CURVE` import still unwraps to the authoritative 3D curve and discards the pcurve list.

## Validation

- X1/X1B focused orientation and boundary-policy suite: 20/20 passed, including explicit-role selection, absent-evidence fallback, partial-evidence rejection, modulo-period equality, and STC-06 ambiguity.
- NIST STEP export/import round-trip corpus: 17/17 passed.
- NIST display-orientation corpus: 16/16 passed.
- Difference Engine, line/arc mass, and selected PMI/stable-identity suites: 96/96 passed.
- Chamfer CLI: 7/7 passed.
- Sheet metal: 99/99 passed.
- Full `Aetheris.slnx` build: succeeded with 0 errors; only existing WebAssembly/analyzer warnings were reported.
- A serialized full-solution test attempt did not complete: it emitted no event after entering `Aetheris.CLI.Tests` for more than five minutes and was stopped. The bounded CLI fixture suite above is the available CLI regression evidence; this report does not represent the aggregate solution test run as passing.
- Ground-truth CLI inspection of FTC-11 succeeds and reports one enclosed manifold with 42 faces, 104 edges, and 72 vertices.
- Fresh-agent periodic diagnosis identified the full-wrap cylinder zero-area/singular-normal failure and rejected `same_sense` repair.
- Fresh-agent degenerate review confirmed that zero 3D length can carry nonzero UV closure and identified the remaining pcurve/`VERTEX_LOOP` loss.
- Fresh-agent code review rejected per-face negative-volume flipping and located support-boundary derivation/export role ownership as the correct layer.

Canonical NIST hashes remain unrebaselined pending full acceptance of pcurve and explicit pole/apex topology preservation.

## Remaining bounded blocker

The topology model already supports per-coedge pcurves, but STEP import does not populate them. Spherical and conical-apex `VERTEX_LOOP`s are still intentionally omitted, and export preflight rejects several explicit zero-length edge representations. Completing those paths requires a separately bounded ingestion/export change with hostile seam-side and pole/apex fixtures; it must not be simulated by source `same_sense`, random face flips, or snapshot updates.
