# HOLE-X1 semantic hole feature scaffold

HOLE-X1 adds a production AIR-owned semantic scaffold for simple shaft holes. A hole is represented as manufacturing/authoring intent before any profile stack, cylinder, trim loop, BRep face, or safe boolean lowering detail is chosen.

## Supported in HOLE-X1

- A semantic `AirHoleFeature` with stable feature name and id.
- Optional target body id.
- Face-local placement on a stable planar entry face selector/name.
- Face-local center coordinates (`U`, `V`) and an explicit frame convention.
- Axis direction, including whether it defaulted from the entry-face normal.
- Simple shaft diameter and derived radius.
- End conditions:
  - `throughAll`.
  - fixed `depth`.
- AIR diagnostics and provenance.
- A deterministic non-executing simple-shaft lowering candidate that keeps the semantic feature attached and reports that executable profile-stack/BRep materialization is deferred.

## Intentionally not supported yet

HOLE-X1 does not implement counterbore, countersink, thread/tap geometry, standard/fit libraries, drill-tip geometry, hole groups, patterns, arbitrary datum placement, `upToFace`, `upToNext`, non-planar entry faces, multi-body propagation, or a general 3D boolean authoring surface.

## Reuse and non-reuse

The scaffold reuses prior vocabulary from the recovery/materializer work at the concept level: shaft radius/diameter, through versus blind/depth semantics, profile-stack lowering as an execution lane, provenance, and deterministic diagnostics. It does not move `HoleRecoveryPlan`, `HoleRecoveryPolicy`, FrictionLab `AirProfileStackExtrude`, or `ProfileStackExtrudeSpec` wholesale into the source/AIR authoring model.

This avoids baking recovery-only constraints into the production semantic contract, especially rectangular-box-only hosts, Z-axis-only holes, CIR recovery as the source of truth, or profile-stack layers as the primary authored representation.

## Relationship to `07-holes-are-semantic-features.md`

The semantic rule is that a hole remains a feature until a lower execution layer deliberately derives geometry. HOLE-X1 codifies that split by making `AirHoleFeature` the AIR boundary object and making the current lowering output a candidate plan that explicitly preserves the source feature instead of replacing it with an anonymous cylinder cut.

## Why intent must survive before lowering

Manufacturing features carry identity, placement, end-condition, and provenance that a bare cylinder or BRep subtraction cannot reliably express. Preserving that intent lets later milestones add stack components, standards, PMI, diagnostics, and controlled lowerers without changing the authoring contract or forcing recovery machinery to guess what the user meant.

## HOLE-X2 continuation

HOLE-X2 adds an executable simple-shaft materialization lane for the scaffold introduced here. See `docs/implementation/hole-x2-simple-shaft-hole-materialization.md` for the bounded profile-stack/BRep route, provenance story, and remaining deferred hole-family scope.
