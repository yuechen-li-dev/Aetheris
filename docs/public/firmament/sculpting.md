# Bounded mathematical sculpting

SURF-X0 supports bounded mathematical sculpting and locality-preserving region modification. It does not provide full general surfacing.

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

The X0 housing emits only `PLANE` and `CYLINDRICAL_SURFACE`. A final STEP scan must report `RationalNURBS = 0`; otherwise export fails.

## Current limits

SURF-X0 does not support general NURBS authoring, arbitrary freeform patches, Loft, Trim/Extend, G1/G2 patch networks, arbitrary deformation, global surface replacement, or arbitrary imported-face sculpting. The first materializer is intentionally limited to a rectangular housing with a bounded rectangular crown and through mounting holes. PMI and Assembly Interface persistence through this lane remain follow-up work; X0 preserves the named mounting-interface and hole-pattern semantic contracts but does not claim new AP242 PMI types.
