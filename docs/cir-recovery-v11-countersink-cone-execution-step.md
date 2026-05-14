# CIR-RECOVERY-V11: CountersinkVariant via CirConeNode + executor + STEP smoke

Bounded supported shape: `Subtract(Subtract(Box,Cylinder),Cone)` with translation-only wrappers.

- `CountersinkVariant` now detects canonical countersink with `CirConeNode`.
- Coaxial XY, entry-face touch, cone ordering, transition radius compatibility, strict clearance, and non-through cone depth are enforced.
- Produces `HoleRecoveryPlan` with conical + cylindrical profile stack and `HoleKind.Countersink`.
- `HoleRecoveryExecutor` now executes countersink by subtracting through cylinder then cone.
- Cone construction convention used: `FirmamentPrimitiveExecutor.ExecuteCone(bottomRadius, topRadius, height)` emits a Z-axis revolve then translates by `-height/2`, so resulting cone spans `[-h/2,+h/2]` with bottom radius at lower Z and top radius at upper Z.
- STEP smoke remains through `Step242Exporter.ExportBody(...)` unchanged; expected `MANIFOLD_SOLID_BREP`, `CONICAL_SURFACE`, `CYLINDRICAL_SURFACE`, and no `BREP_WITH_VOIDS` for open countersink.

Non-goals preserved:
- no threaded/generic profile stack recovery,
- no exporter behavior changes,
- no topology naming expansion (SEM-A0 preserved).
