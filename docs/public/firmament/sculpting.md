# Bounded mathematical sculpting and patch replacement

SURF-X0 supports bounded mathematical sculpting and locality-preserving region modification. SURF-X1 adds one deliberately narrow freeform lane: replace the rectangular center of the admitted housing top with a bounded tensor-product patch. This is not full general surfacing.

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

`TrimRegion` currently restricts a non-rational patch to a subdomain already present in its knot support. `ExtendRegion` can restore or enlarge that bounded domain only inside the same support. Arbitrary extrapolation, surface-surface intersection trimming, holes in patch loops, and coincident/tangent-support classification are not yet public capabilities.

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

The admitted X1 materializer is limited to a single rectangular outer loop in the center of the canonical planar housing top. It does not provide general imported-face replacement, inner patch loops, arbitrary intersection/trim solving, general surface extension, G2, Blend, Loft, Shell, Draft, fitting, fairness optimization, or arbitrary patch networks. Analytic surface objects exist in the patch IR but direct Firmament patch authoring currently admits the non-rational B-spline form only.

The named mounting interface and per-hole semantic identities survive the operation. Authored AP242 PMI and Assembly Interface association round-trip through this sculpting lane is not yet implemented and is not claimed.
