# LANG-SPAN-SURFACE-X1 — bounded surface support semantics

## Executive verdict

**Meaningful progression.** `Span<Plane>` is now a real bounded engineering support for `Hole`, `Boss`, `Pocket`, and finite Pattern expansion without becoming separate topology. Feature placement, containment, provenance, STEP export/reimport, determinism, and packaged-CLI parity are qualified. FEA selection and PMI association remain deferred because their current authorities require exact analysis/BRep region bindings or admitted face/hole targets; substituting the parent face would violate the bounded-support law.

## Pre-change audit

At baseline commit `e920321e`, `GeometricSpanView` stored Span identity/type, parent identity/type, textual domain, orientation, endpoints or boundary Profile name, optional curve length, provenance, and an internal curve for curve spans. `Span<Plane>` retained only the names of a Construction Plane and boundary Profile. The boundary was semantic-only: it was not resolved, validated, measured, or materialized. Consumers could not distinguish bounded support from the parent because no feature, FEA, or PMI consumer bound the Span.

The reusable authorities were already present: `ResolvedProfile2DValidator` owns closed, planar, non-self-intersecting Profile validity; `ProfileArrangementBuilder.PointInProfile` owns analytic line/arc point classification including inner loops; Profile arrangement already proves actual Profile containment for prismatic features; and Hole/Boss/Pocket already lower through the section-stack construction. Surface containment therefore belongs at semantic consumer binding, before BodyState mutation, using those authorities. No new geometry backend is needed.

FEA normalizes a `SemanticReference` only when it carries `BoundaryRegionCapability` and an `ExactAnalysisRegionBinding`, `ExactBrepFaceBinding`, or `ExactBrepRegionBinding`. PMI validation admits face selectors, named holes, aliases, and recognized regions. Neither baseline seam represented a bounded Profile domain, so neither could be honestly enabled by name recognition alone.

## Semantics and policy

A planar Span contains the points lying on its referenced Construction Plane and inside its referenced Profile. It retains the parent object, resolved boundary, inherited local frame and normal, orientation, boundary identity, area, provenance, and consumers. It does not copy the plane. Rebinding resolves both names on every compile, so parent/boundary edits propagate and missing or invalid identities fail.

The boundary must pass ordinary Profile validation. A Profile authored in `XY` or in a `Concept Struct ... On XY` layout is interpreted as parent-local 2D geometry; an explicitly different Construction Plane is rejected with `firmament-span-surface-boundary-off-parent:<span>`. Exact boundary points are boundary-valid under the Profile arrangement tolerance (`1e-7 mm`). Area is deterministic outer-loop area minus inner-loop areas.

Point containment delegates to the established arrangement classifier. Circular Hole footprints additionally use exact minimum distance to bounded line, arc, and circle curves, so center-inside is insufficient. Boss/Pocket profiles use the existing arrangement-based full-footprint containment proof. The nonrectangular diamond and circular-arc tests prevent AABB substitution; the ring test proves inner-loop exclusion.

## Support matrix

| Consumer | Span accepted | Containment | Provenance retained | External artifact |
| --- | --- | --- | --- | --- |
| Hole Shaft/Counterbore | yes | complete circular footprint; largest counterbore governs | Span and parent, plus signed boundary margin | existing AP242 body geometry; no Span reconstruction |
| Boss | yes | actual Profile footprint | Span and parent | existing AP242 body geometry |
| Pocket | yes | actual Profile footprint | Span and parent | existing AP242 body geometry |
| Pattern | yes | every expanded instance | generated instance plus Span and parent | finite Pattern report and existing geometry |
| FEA Fixed | no | deferred | no fallback | none |
| FEA Force/Pressure/Traction | no | deferred | no fallback | none |
| PMI | no | deferred | no fallback | AP242 remains face/hole-level only |

Boss/Pocket support is qualified only for the existing prismatic top-support route. Imported faces and nonplanar surfaces remain outside X1.

## Topology and semantics

```text
Plate.Top Face
    one topological face

MountingArea Span<Plane>
    parent: TopSupport
    boundary: MountBoundary
    semantic bounded region within the top support
```

No face is split. The Span is semantic support authority; it becomes product geometry only indirectly when an admitted feature changes the body.

## Feature evidence

The canonical flagship is `fixtures/Canonical/Span/plane-hole-support.firmament`: a 100 x 80 x 8 mm plate, a 2,880 mm2 diamond mounting Span, and a finite four-instance Pattern of M8 counterbores. Each instance preserves `SupportSpan=MountingArea`, `ParentSupport=TopSupport`, and a positive 1.4715918833 mm minimum margin. It lowers through the existing `PrismaticSectionStack/Remove` route and materializes an enclosed manifold.

`fixtures/Invalid/Span/plane-hole-footprint-crossing.firmament` moves Pattern instance 3 so the 16 mm counterbore disk crosses the diamond. Compilation fails before construction with `firmament-feature-footprint-outside-span:MountPattern_3:MountingArea`, center `[28,9]`, radius `8`, and margin `-5.482693553`; no STEP is emitted. Tests also prove that the same location on whole `+Z` support passes, shrinking only the boundary invalidates a previously valid feature, and moving the parent plane and host together preserves local containment while moving feature extents.

Boss and Pocket witnesses validate rectangular Profile footprints, preserve both support identities, and use their existing Add/Remove lowering. A profile crossing the Span produces the same typed footprint diagnostic.

## Export and determinism evidence

The flagship exports through the existing AP242 path. Reinspection reports one enclosed manifold body, 46 faces, 116 edges, and 76 vertices; surfaces are 14 planes and 32 cylinders, curves are 52 lines and 64 circles, with zero B-spline, rational, unsupported, or faceted fallback. Two repository exports and a fresh `dotnet publish` CLI export were byte-identical at SHA-256 `B894DF6FA97216F2DF847CB503A81248991E67970A7E198D9CC3828E264913D2`.

STEP intentionally carries the resulting body geometry, not an invented sub-face association. Rich Span identity remains in source/build inspection.

## Deferred analysis and PMI

FEA cannot be qualified by passing `MountingArea` as a path string: quadrature and node selection obtain exact selected area and normal from `IPlanarBoundaryDomainCapability`, while semantic normalization requires an exact analysis/BRep region binding. A bounded Profile-to-continuum binding and empty-selection policy are the next real work. Applying the load or constraint to the parent face would be incorrect.

PMI has no generic annotation/leader record in the admitted grammar. Datum and geometric-tolerance targets ultimately require current face/recognized-region semantics, while HoleDiameter targets holes. X1 therefore reports Span consumers for features only and makes no AP242 sub-face association claim.

## Validation

Local qualification completed:

- Release solution build: passed, zero warnings and zero errors.
- Firmament suite: 1,366 passed, zero failed or skipped; focused surface-Span lane: 10 passed.
- CLI suite: 416 passed, zero failed or skipped; focused surface-Span CLI lane: 6 passed.
- FEA regression suite: 27 passed, zero failed or skipped.
- Canonical and invalid CLI validation, `inspect-spans`, feature inspection, actual materialization, STEP export/reimport, deterministic repeat, and fresh packaged-CLI parity: passed.
- Repository layout guard: passed for 3,988 tracked files; `git diff --check`: passed.

Future bounded work is `LANG-SPAN-SURFACE-X2` for cylinder/cone/B-spline domains, `LANG-REGION-X1` for composite or disconnected regions, and `IMPORT-SPAN-X1` for imported BRep supports. None is implemented here.
