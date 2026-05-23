# CIR-RECOVERY-V16 — bottom-entry support for bounded `ChamferedEntryHoleVariant`

## Scope

V16 extends the existing bounded chamfered-entry lane from V15 to include mirrored **bottom-entry (-Z)** conical entry relief, while preserving all existing policy architecture and exporter behavior.

Included:
- `HoleRecoveryPolicy -> ChamferedEntryHoleVariant` now recognizes top and bottom entry chamfer cones for canonical bounded nested subtract shape.
- `HoleRecoveryExecutor` chamfer/countersink-like execution now resolves cone placement for top or bottom entry from plan/host z extents.
- Focused tests for plan admission, execution, STEP smoke, anti-steal behavior, and invalid bottom-cone orientation rejection.

Not included:
- new policy families
- generic chamfer recovery
- STEP exporter changes
- topology naming expansion (SEM-A0 preserved)

## Supported CIR shape (bounded)

```text
Subtract(
  Subtract(Box, HoleCylinder),
  ChamferCone
)
```

Where:
- `Box` is rectangular host.
- `HoleCylinder` is coaxial Z-axis cylindrical base hole.
- `ChamferCone` is `CirConeNode`.
- direct or identity/pure-translation wrappers are allowed.
- no rotation/non-translation transforms.

## Entry-side and cone radius convention

Entry-side detection:
- top-entry if `coneMaxZ == boxMaxZ` within tolerance
- bottom-entry if `coneMinZ == boxMinZ` within tolerance
- touching neither or both entry faces is rejected.

Radius interpretation is entry-relative:
- top-entry: `entryRadius = cone.TopRadius`, `transitionRadius = cone.BottomRadius`
- bottom-entry: `entryRadius = cone.BottomRadius`, `transitionRadius = cone.TopRadius`

Admissibility requires:
- strict decreasing radius from entry to transition,
- transition radius matches cylinder radius within tolerance,
- chamfer thresholds (depth/radius-delta) pass,
- strict host XY clearance at max radius.

## Plan / execution semantics

`ChamferedEntryHoleVariant` emits `HoleRecoveryPlan` with:
- `HoleKind.ChamferedEntry`
- `HoleEntryFeatureKind.Chamfer`
- conical segment then cylindrical segment profile stack.

`HoleRecoveryExecutor` uses existing bounded route:
1. build host box
2. subtract through cylinder
3. build cone tool
4. resolve cone center from detected entry side (top or bottom)
5. subtract cone

Result remains manifold `BrepBody` route suitable for STEP242 export.

## Countersink/chamfer anti-steal behavior

Preserved for both top and bottom entry:
- countersink rejects chamfer-sized cones (`RejectedChamferSizedEntryRelief`)
- chamfer rejects countersink-like cones (`RejectedCountersinkLikeCone`)

## STEP smoke expectations

Bottom-entry chamfer execution exports through existing `Step242Exporter.ExportBody(...)` with expected markers:
- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `CONICAL_SURFACE`
- `CYLINDRICAL_SURFACE`

And no `BREP_WITH_VOIDS`.

## SEM-A0 status

No generated topology naming/provenance expansion introduced. V16 is bounded to entry-side hardening inside existing hole-family variant architecture.

## V17 follow-up
Bottom-entry chamfer segments now participate in the shared explicit placement contract validator.
