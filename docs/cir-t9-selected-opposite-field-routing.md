# CIR-T9 selected-opposite field routing

## What T8.1 left incomplete
T8.1 deterministically routed retained loops to a selected opposite descriptor, but trim-oracle restricted-field evaluation could still use a broad opposite operand subtree.

## T9 change
T9 adds `SelectedOppositeFieldBuilder` so retained-loop trim-oracle integration attempts to reconstruct a CIR field from the selected opposite descriptor first.

- when selected opposite field builds: `oracle-trim: selected-opposite-field-used` and strong evidence is allowed
- when selected opposite field cannot build: `oracle-trim: selected-opposite-field-deferred:<reason>` and strong evidence is blocked
- broad opposite routing remains diagnostic-only (`oracle-trim: broad-opposite-field-only`)

## Primitive support status
- Cylinder: supported when `CylindricalSurfaceGeometryEvidence` is canonical and axis alignment is representable under current transform guardrails.
- Sphere: deferred (`selected-opposite-field: sphere-geometry-missing`) because descriptor currently lacks center/radius evidence.
- Torus: deferred (`selected-opposite-field: torus-geometry-missing`) because descriptor currently lacks major/minor/frame evidence.

## Semantics
Strong/materialization-grade oracle evidence now requires both:
1. deterministic selected opposite descriptor
2. restricted field built from that selected opposite descriptor

Broad-only fields never claim strong evidence.

## Non-goals preserved
- no BRep topology emission changes
- no STEP export behavior changes
- no BSpline fitting or torus quartic/exact recognition claims
- no Boolean behavior expansion

## Recommended T10
Add spherical/toroidal descriptor geometry evidence (center/radius and major/minor/frame respectively) so selected-opposite field reconstruction can promote those families from deferred to strong where valid.
