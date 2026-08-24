# SURF-X4 — continuous section chains

## Executive verdict

**Accepted for the bounded SectionChain lane.** Aetheris can materialize changing-profile G1 SectionChains with compiler-derived, world-space tangent fields, exact non-rational polynomial product surfaces, shared BRep topology, deterministic candidate selection, and bounded edit/replay dependencies. G2, rail lofts, global surface fitting, topology-changing correspondence, and general freeform Booleans remain outside the claim.

## Architecture audit

1. **Reused from BlendBoundary:** hard continuity eligibility before scoring, deterministic `JudgmentEngine` selection/manual-policy structure, bending/normal-variation quality concepts, non-rational representation gates, and independent realized-boundary verification.
2. **Reused non-rational construction:** `BSplineSurfaceWithKnots`, `BSpline3Curve`, STEP polynomial B-spline export/import, BRep face/edge bindings, shared topology, and bounded pcurve construction/validation.
3. **Specialized for SectionChains:** semantic-span control correspondence, frame-to-world conversion, cumulative frame-origin chord spacing, a nonuniform three-section derivative stencil, one-sided endpoint tangents, pairwise Hermite control nets, longitudinal seam curves, overshoot/foldover qualification, and bounded edit locality.
4. **Kept different from pairwise boundary blending:** BlendBoundary's planar vanishing-power shape is not a longitudinal tangent estimator. SectionChain does not replace a region against an analytic shoulder and does not request G2 normal-curvature matching. It interpolates every authored station and shares one tangent direction between two neighboring pairwise patches.
5. **Untouched Ruled paths:** explicit G0 SectionChains still use `RuledSurfaceIr`/`RuledSurfaceLowering`; old Firmament fixtures with `Transition: Ruled` migrate to G0; Sweep/Paperclip remains on its cylinder/torus/plane path; X3b sources without a continuity field retain G0 replay semantics.

## Tangent derivation and judgment

At section `i`, corresponding polynomial controls are first transformed through their complete section frames. Interior derivatives are the derivative at the middle sample of the nonuniform quadratic through sections `i-1`, `i`, and `i+1`. Station parameter is cumulative Euclidean distance between frame origins. Endpoints use the first/last one-sided chord derivative.

Each transition is a cubic Hermite/Bezier polynomial in the longitudinal parameter. Candidate magnitude scales are `ConservativeCompact = 0.5`, `CentripetalLike = 0.75`, and `ChordLengthFair = 1.0`. Foldover and normalized envelope overshoot are hard rejection gates. Eligible candidates are scored deterministically from bending energy, normal variation, and compactness; ties use declared policy order and ordinal name. Continuity is independently sampled after construction and cannot be traded for quality.

The selected flagship policy and all raw metrics are emitted by `section-chain inspect/build --json`; constants are centralized in `SmoothSectionChainBuilder`.

## Representation and topology

The eight-section flagship has 7 transitions and 28 polynomial transition faces. Its line spans are exactly degree-elevated and its existing cubic profile spans retain compatible knots. The longitudinal degree is 3; no rational weights exist. Adjacent patches reuse actual section edges and vertices, while each coedge receives its own face-local pcurve. Terminal planar caps are claimed only as G0.

The product boundary remains:

```text
RationalProductSurfaces = 0
FacetedCanonicalFallback = 0
```

Circular-arc G1 spans and incompatible polynomial degrees/knot structures currently fail closed with `section-chain-g1-degree-limit`; they are not rationalized.

## Locality and construction state

G0 edit locality remains the two adjacent transitions. With the G1 three-section stencil, editing `Rise` recomputes tangent fields `PalmFront`, `Rise`, and `Peak`, and rebuilds `Front->PalmFront`, `PalmFront->Rise`, `Rise->Peak`, and `Peak->PalmRear`. `Nose->Front`, `PalmRear->Rear`, and `Rear->Tail` remain preserved. This set is recorded by `SectionChainEditDelta`.

`SectionChain` stores sections, frames, profiles, correspondence, `Continuity`, `TransitionPolicy`, and `SmoothPolicy`; tangent fields and control nets are derived evidence rather than semantic authority. Existing typed `AddSectionChain`/`RemoveSectionChain` construction-state payloads therefore replay the intent. The bounded housing builder consumes the exact transition supports and longitudinal curves produced by the same materializer.

## Before/after and reproduction

The two flagship commands use the identical eight-section definition; only continuity/transition intent differs:

```powershell
dotnet run --project Aetheris.CLI -c Release -- section-chain build flagship-g0 --out artifacts/local/surf-x4/surf-x4-ergonomic-g0.step --json
dotnet run --project Aetheris.CLI -c Release -- section-chain build flagship --out artifacts/local/surf-x4/surf-x4-ergonomic-g1.step --json
```

The G0 artifact has straight ruled generators and visible station tangent changes. The G1 artifact interpolates the same profiles using shared cross-section tangent directions. Machine-specific STEP files, evidence JSON, and previews remain under ignored `artifacts/local/surf-x4/` per generated-artifact policy.

The preview implementation is now a shared Kernel.Core BRep renderer rather than SectionChain-specific code. `aetheris wireframe model.step` reimports any qualified STEP body, recovers bounded pcurves when required, draws authoritative topology curves, and clips exact-support isolines against trim loops. SectionChain sibling previews consume this same path.

## Qualification

Release validation covers Release build, SectionChain G0 regressions, G1 flagship construction, nonuniform spacing via its unequal flagship chord intervals, world-space twisted frames, semantic correspondence, endpoints, foldover/self-intersection, overshoot candidate qualification, independent G0/G1 metrics, pcurves/shared topology, zero rational STEP output, reimport, deterministic repeat export, Firmament G0/G1 binding, and bounded edit locality. The full serial repository suite is the final regression gate, including SURF-X2/X3/X3b and Paperclip/Sweep tests.

## Manual review checklist

Compare the G0 and G1 artifacts from one camera and inspect section bands, highlight continuity, flat spots, pinching, bulging/overshoot, nose/tail behavior, silhouette quality, and whether the result reads as one smooth sculpted mass. For G1 grips inspect attachment and remote preservation; for ducts inspect internal flow, membranes, and constrictions.
