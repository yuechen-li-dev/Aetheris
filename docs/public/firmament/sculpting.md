# Bounded mathematical sculpting and patch replacement

SURF-X0 supports bounded mathematical sculpting and locality-preserving region modification. SURF-X1 adds direct rectangular patch replacement. SURF-X1a closes the trim path for the admitted housing: qualified surface intersections derive the outer boundary, face-local pcurves qualify every trim, circular inner loops preserve openings, and an imported `ADVANCED_FACE` can receive a bounded support-surface graft. This is not full general surfacing.

Sculpting is a lower-authority escape hatch for geometry that an engineering feature such as `Boss`, `Pocket`, `Hole`, `Sweep`, `Fillet`, or `Chamfer` cannot express. Prefer those features whenever they carry the intended engineering meaning.

## Body states

Every accepted operation consumes exactly one immutable semantic `BodyState` and produces a new one:

```text
Base -> CrownRaised
Base -> CrownHigh
```

Both variants can share `Base`; neither mutates it or the other. State IDs are deterministic hashes of the predecessor and canonical operation contract. Ordinary authoring always references the current predecessor, not topology from an arbitrary historical state.

## X0 authoring

The current operation is `OffsetRegion`. The canonical controller-cover example is [`fixtures/Canonical/Sculpting/sculpted-housing.firmament`](../../../fixtures/Canonical/Sculpting/sculpted-housing.firmament):

```firmament
SculptState CrownRaised {
  Input: Base
  OffsetRegion {
    Target: HousingCrown
    Offset: 6mm
    Region: [60mm, 40mm]
    InfluenceEnvelope: [-50mm, -40mm, 20mm, 50mm, 40mm, 26mm]
    Boundary: G0
  }
  MayModify: [HousingCrown, CrownTransitionZone]
  Preserve: [BottomMountingInterface, MountingHolePattern, OuterFootprintBoundary]
  Require: [ClosedManifold, OrientationConsistency, NoSelfIntersection]
}
```

`MayModify` grants authority; `Preserve` requires semantic identity and the declared geometry fingerprint to survive; `Require` is a postcondition on the result. They are deliberately different contracts. The X0 influence envelope is a conservative axis-aligned volume, not a general spatial-logic language.

`Region` is ordered `[width, depth]` and bounds the raised crown plateau. The larger influence envelope must also contain the authorized G0 transition zone; in the flagship it covers the full footprint above `z=20`.

The accepted operation records reads, preserved/replaced/introduced/removed entities, the authorized domain, correspondence, and validation evidence in `GeometricDelta`. `aetheris inspect model.firmament --json` reports every state. `aetheris build model.firmament --json` also writes a sibling `.delta.json` artifact.

`build` performs export preflight and the rational-surface product-boundary guard; it does not report an independent STEP reimport. Run `aetheris analyze result.step --json` after building to exercise the production importer and report the reconstructed topology, surface inventory, and bounds.

## X1 patch authoring

Use normal engineering features for normal CAD. `SurfacePatch` and `ReplaceRegion` are advanced sculpting tools for geometry that exact engineering features cannot express. The canonical witness is [`surf-x1-freeform-housing.firmament`](../../../fixtures/Canonical/Sculpting/surf-x1-freeform-housing.firmament).

```firmament
SurfacePatch CrownPatch {
  Degree: [3, 3]
  Domain: [0, 1, 0, 1]
  KnotsU: [0, 0, 0, 0, 0.3333333333333333, 0.6666666666666666, 1, 1, 1, 1]
  KnotsV: [0, 0, 0, 0, 0.3333333333333333, 0.6666666666666666, 1, 1, 1, 1]
  ControlRow: [[-30mm, -20mm, 20mm], ...]
  # Six rows of six finite control points in the complete fixture.
  Boundary South { Existing: CrownBoundarySouth
    Continuity: G0 }
  Boundary East { Existing: CrownBoundaryEast
    Continuity: G1 }
  Boundary North { Existing: CrownBoundaryNorth
    Continuity: G0 }
  Boundary West { Existing: CrownBoundaryWest
    Continuity: G1 }
}

SculptState FreeformCrown {
  Input: Base
  ReplaceRegion {
    Target: HousingCrown
    Patch: CrownPatch
    InfluenceEnvelope: [-30mm, -20mm, 20mm, 30mm, 20mm, 28mm]
  }
  MayModify: [HousingCrown, CrownTransitionZone]
  Preserve: [BottomMountingInterface, MountingHolePattern, OuterFootprintBoundary, SideWallsLower]
  Require: [ClosedManifold, OrientationConsistency, NoSelfIntersection]
}
```

The public patch carries degree, rectangular control net, expanded nondecreasing knots, stable `[uMin,uMax] x [vMin,vMax]` domain, one outer loop, four explicit old-boundary correspondences, per-edge G0/G1 contracts, orientation, and export class. It has no weights field. `inspect --json` reports this metadata without dumping the control net.

G0 compares 33 deterministic samples to the exact preserved boundary line. G1 additionally compares patch tangent planes to the planar frame and reports the maximum angular error. The flagship requires G1 on east/west and G0 on north/south. Accepted patch edges and vertices are shared BRep topology, not merely nearby geometry.

## X1a trim closure

The canonical trimmed witness is [`surf-x1a-trimmed-freeform-housing.firmament`](../../../fixtures/Canonical/Sculpting/surf-x1a-trimmed-freeform-housing.firmament). Its non-rational patch spans the full top, owns one outer loop and four circular inner loops, and leaves the four cylindrical hole walls as preserved neighbors. The outer spline edges come from four qualified Plane/non-rational-B-spline boundary intersections rather than four unrelated duplicate curves.

A pcurve is the same trim edge expressed in one face's `(u,v)` coordinates. It is essential because a 3D curve alone does not say how it bounds a parameterized surface. A shared edge therefore has one topological edge and one 3D curve, but two face-local pcurves—one on each adjacent support. Aetheris independently samples `Surface(pcurve(t))` against `Curve3D(t)`, checks parameter domains and orientation, and blocks export with `surf-pcurve-invalid` on disagreement. AP242 export uses `SURFACE_CURVE` and `PCURVE`, not metadata-only trim hints.

The qualified intersection matrix is intentionally narrow:

| Pair | Qualified result |
|---|---|
| Plane / Plane | Exact line for transverse planes; explicit no-intersection and coincident-region classification |
| Plane / Cylinder | Exact circle when the plane is normal to the cylinder axis |
| Plane / non-rational B-spline | Exact clamped boundary isoparametric B-spline curve |
| Cylinder / non-rational B-spline | Pcurve generation is qualified for an existing shared circle; general intersection discovery is deferred |
| non-rational B-spline / non-rational B-spline | Deferred |

Multiple branches require a seed/reference boundary. Admissibility, score, and tie-breaking go through deterministic utility scoring; an unseeded ambiguous result fails instead of selecting the kernel's first curve.

`TrimRegion` restricts an existing non-rational support. Bounded support extension distinguishes exact analytic continuation for Plane/Cylinder/Cone from degree-3-or-lower endpoint-tangent continuation for a non-self-intersecting non-rational B-spline. Extension is limited to 25% of the original span per side, reports the old/new domain and boundary continuity law, and does not enlarge the final solid outside the authorized trim. Larger or unstable requests fail with `surf-extension-unsupported`.

The advanced imported route first imports a STEP body, selects exactly one `FaceGeometryBinding.SourceStepEntityId`, and grafts the new support onto that current face. It retains the imported topology object, vertices, edge curves, neighboring face bindings, and neighboring source entity IDs. The graft then rebuilds and validates pcurves. This is a bounded `ADVANCED_FACE` route, not arbitrary dumb-solid editing or reconstruction into native primitives.

Imported rational surfaces follow the strict normalized re-export policy: Aetheris does not pass foreign rational product surfaces through a newly authoritative STEP merely because they were untouched. They must first normalize to an exact analytic or supported non-rational representation; otherwise import/materialization or export blocks explicitly. Newly authored replacement geometry is always subject to the same no-rational-product boundary. The canonical X1a import/replacement witness contains zero rational surfaces before and after replacement.

The advanced API sequence is:

```csharp
var imported = Step242Importer.ImportBody(stepText).Value;
var adopted = ImportedFaceRegionReplacer.AdoptImportedBody(baseState, imported, "ImportedBaseStep");
var result = ImportedFaceRegionReplacer.Apply(adopted, sourceAdvancedFaceId, patch, "ImportedTrimmedCrown");
```

`sourceAdvancedFaceId` must resolve to exactly one current imported face. Reusing it after replacement fails with `surf-selector-target-replaced`, even though the original STEP entity ID remains on the successor binding as inspectable provenance.

The X1a `BodyState` also carries explicit `PersistentGeometryAssociation` entries. The bottom datum, hole-diameter/position PMI, and `BottomMountingInterface` bind to current face IDs. STEP export emits AP242 `GEOMETRIC_ITEM_SPECIFIC_USAGE`; `Step242SemanticPmiInspector` must recover non-empty face associations. Only `Preserved` or explicit `ReplacedBy` correspondence may rebind an association—name matching and nearest-face search are prohibited.

For the canonical artifact, `aetheris analyze result.step --json` reports these associations under `semanticPmi.items[*].geometricFaceEntityIds`. Datum A and the assembly-interface annotation each bind to the bottom face; diameter and position items each bind to all four cylindrical faces. The replacement B-spline face has five loops (one outer plus four inner). The complete body emits 24 `SURFACE_CURVE` and 48 `PCURVE` entities because all 24 shared edges carry two face-local pcurves; the eight crown trim edges account for 16 of those pcurves.

An ordinary safe cylindrical `HoleFeature` can follow replacement on a surviving current-state region. Selectors are resolved against the immediate predecessor. A selector for removed `HousingCrown` fails with `surf-selector-target-replaced` and names `CrownPatch` and the replacing state.

For low/high freeform alternatives, declare two complete `SurfacePatch` values and two sibling `SculptState` blocks whose `Input` is the same base state. Do not derive the high version from the accepted low version or edit an accepted control net in place. A model has one `Output`; select the sibling to build there, or keep separate small model files when both STEP variants are release artifacts.

## Guarantees and evidence

The housing lane verifies:

- exact semantic preservation of the bottom mounting interface, mounting-hole centers/axes/diameters, and lower footprint;
- exact analytic equality below the crown boundary, with maximum outside-domain deviation reported as zero;
- G0 reconnection through four analytic transition planes;
- BRep binding/preflight validity, edge-incidence enclosure, and orientation consistency before commit;
- failure atomicity: rejected operations never produce an accepted output state;
- STEP reimport and a post-serialization rational-surface scan.

`NoSelfIntersection` in X0 is a certified bounded guarantee for the admitted rectangular crown construction. It is not advertised as a universal intersection theorem.

## Product surface boundary

**Aetheris does not use NURBS as its universal surface abstraction.**

Internal algorithms may temporarily use rational spline mathematics, but exported product geometry must normalize to analytic or non-rational forms. The export order is exact analytic surface, exact or explicitly bounded non-rational B-spline, then a blocking diagnostic. Arbitrary rational NURBS fallback is prohibited. The diagnostic is `surf-surface-export-normalization-failed`.

The X0 housing emits only `PLANE` and `CYLINDRICAL_SURFACE`. The X1 flagship adds one `B_SPLINE_SURFACE_WITH_KNOTS` with no rational complex entity. A final STEP scan must report `RationalNURBS = 0`; otherwise export fails.

> Non-rational B-spline surfaces are an advanced product representation used only when exact analytic geometry is insufficient.

> Rational NURBS remain an internal computational mechanism only. They are never a product-boundary fallback.

## Current limits

The admitted X1a materializer is limited to rectangular housing-top replacement, four qualified boundary intersections, and circular inner openings whose existing 3D edges lie on the replacement support. It does not provide arbitrary intersection networks, general Cylinder/B-spline or B-spline/B-spline discovery, arbitrary nested islands, G2, Blend, Loft, Shell, Draft, fitting, fairness optimization, or arbitrary patch networks. Direct Firmament patch authoring admits the non-rational B-spline form only; imported replacement is an advanced API path over an already imported BRep.
