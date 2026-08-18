# AIR-X3 — Primitive-as-AIR normalization audit

## 1) Executive summary

This audit concludes that the primitive-as-AIR thesis is viable in Aetheris for bounded analytic solids, but migration risk is strongly topology-dependent.

- **Lowest risk**: box (rectilinear extrude) and cylinder (circular extrude).
- **Moderate risk**: cone/frustum (revolve profile + apex/radius-zero policy).
- **Higher risk**: sphere (single periodic face with pole singularities) and torus (double-periodic seam topology).

Current production already demonstrates that STEP/AP242 export fidelity is primarily driven by emitted analytic surface families (`PLANE`, `CYLINDRICAL_SURFACE`, `CONICAL_SURFACE`, `SPHERICAL_SURFACE`, `TOROIDAL_SURFACE`) and stable topology conventions, not by whether a shape originated in a primitive helper or a sweep/revolve constructor. The practical migration constraint is therefore **topology/seam parity**, not geometric expressiveness.

No production behavior changes are proposed in AIR-X3.

## 2) Source code/docs inspected

### Core primitive and topology emitters
- `Aetheris.Kernel.Core/Brep/BrepPrimitives.cs`.
- `Aetheris.Kernel.Core/Brep/Features/BrepRevolve.cs` (cone/revolve conventions).
- `Aetheris.Kernel.Core/Brep/Features/BrepExtrude.cs` (existing extrude pathway baseline).

### STEP exporter/importer behavior
- `Aetheris.Kernel.Core/Step242/Step242Exporter.cs`.
- `Aetheris.Kernel.Core/Step242/Step242Importer.cs`.
- `Aetheris.Kernel.Core/Step242/Step242SubsetDecoder.cs`.

### Primitive and STEP tests
- `Aetheris.Kernel.Core.Tests/Step242/Step242ExporterTests.cs`.
- `Aetheris.Kernel.Core.Tests/Step242/Step242ConicalSurfaceRegressionTests.cs`.
- `Aetheris.Kernel.Core.Tests/Brep/Boolean/BrepBooleanTests.cs` (cone helper via `BrepRevolve.Create`, analytic-family assertions).

### Firmament/CIR lowering paths
- `Aetheris.Kernel.Firmament/Lowering/FirmamentPrimitiveLowerer.cs`.
- `Aetheris.Kernel.Firmament/Lowering/FirmamentCirLowerer.cs`.

### AIR docs
- `docs/development/milestones/general/air-a0-architecture-probe-report.md`.
- `docs/development/milestones/general/air-v1-profile-stack-extrude-production-scaffold.md`.
- `docs/development/milestones/general/air-v2b-blind-counterbore-interval-production.md`.

## 3) Primitive mapping table

| Primitive | Representable as bounded analytic AIR? | Canonical AIR atom (recommended) | Alternate form(s) | Risk |
|---|---|---|---|---|
| Box | Yes | `AirExtrude` (rectangle profile + linear path) | None worth preferring | Low |
| Cylinder | Yes | `AirExtrude` (circle profile + linear path) | `AirRevolve` (line segment around axis) | Low–moderate |
| Cone/frustum | Yes | `AirRevolve` (radial line segment around axis) | `AirRuledTransition` (circle->circle) | Moderate |
| Sphere | Yes | `AirRevolve` (semicircle/arc around axis) | none compelling in bounded scope | High |
| Torus | Yes | `AirRevolve` (offset circle around axis) | none compelling in bounded scope | Highest |

Preference rule: choose the AIR atom that most directly preserves current topology conventions with minimal seam reinterpretation.

## 4) Proposed minimal AIR atoms

> Records below are architectural sketches only (no production implementation in AIR-X3).

```csharp
internal readonly record struct AirExtrude(
    AirProfile2D Profile,
    AirFrame3D ProfileFrame,
    AirLinearPath3D Path,
    AirCapPolicy Caps,
    AirSeamPolicy Seams,
    AirOrientationPolicy Orientation,
    AirTolerancePolicy Tolerance);

internal readonly record struct AirRevolve(
    AirProfile2D Profile,
    AirFrame3D ProfileFrame,
    AirAxis3D Axis,
    AirAngleInterval Sweep,
    AirCapPolicy Caps,
    AirSeamPolicy Seams,
    AirPolePolicy Poles,
    AirOrientationPolicy Orientation,
    AirTolerancePolicy Tolerance);

internal readonly record struct AirRuledTransition(
    AirProfile2D StartProfile,
    AirProfile2D EndProfile,
    AirFrame3D Frame,
    AirBoundedParameterInterval Span,
    AirCapPolicy Caps,
    AirSeamPolicy Seams,
    AirOrientationPolicy Orientation,
    AirTolerancePolicy Tolerance);
```

Design intent:
- `AirExtrude`: deterministic for box/cylinder.
- `AirRevolve`: canonical for cone/sphere/torus and optional for cylinder.
- `AirRuledTransition`: optional bounded helper for future frustum-specific policy, not required for first migration.

## 5) Per-primitive analysis

### Box
- **Current**: explicit 8 vertices / 12 edges / 6 planar faces in `BrepPrimitives.CreateBox` with deterministic line/plane bindings.
- **AIR representability**: exact via rectangle + linear extrude.
- **Canonical**: `AirExtrude`.
- **Parity requirement**: keep six planar faces and edge orientation layout stable enough for existing boolean recognition and STEP tests.
- **Conclusion**: best first production migration candidate.

### Cylinder
- **Current**: side cylindrical face + two planar caps; explicit seam edge and cap circles in `CreateCylinder`.
- **AIR representability**: exact via circular extrude; also revolve of segment.
- **Canonical**: `AirExtrude` preferred initially because cap policy is explicit and parallels existing constructor semantics.
- **Key risk**: seam placement/orientation and cap loop orientation parity.
- **Conclusion**: second production migration candidate after box.

### Cone/frustum
- **Current**: no direct `BrepPrimitives.CreateCone`; cone bodies are produced through revolve pathways in tests (`BrepRevolve.Create` helper usage) and consumed/exported as conical surfaces.
- **AIR representability**: exact via revolve of radial segment over height.
- **Canonical**: `AirRevolve`.
- **Key risks**: apex policy when one radius is zero, cap omission rules, semi-angle canonicalization.
- **Conclusion**: moderate risk; suitable after box/cylinder migration.

### Sphere
- **Current**: single spherical face with no boundary loops in `CreateSphere` (periodic/singular simplification).
- **AIR representability**: revolve of semicircle/arc.
- **Canonical**: `AirRevolve`.
- **Key risks**: pole handling, seam conventions, and preserving single-face topology that current tests/export lanes rely on.
- **Conclusion**: high risk relative to box/cylinder/cone due to singular topology semantics.

### Torus
- **Current**: one toroidal face with one loop composed by two self-loop seam edges used twice each (double-periodic convention).
- **AIR representability**: revolve of offset circle.
- **Canonical**: `AirRevolve`.
- **Key risks**: dual periodic seam policy, shared seam vertex convention, deterministic loop ordering.
- **Conclusion**: highest migration risk; keep direct constructor longest.

## 6) Topology/seam/cap/pole policy findings

### Cap policy
- Box: always closed caps from profile ends (implicit for prism extrude).
- Cylinder: two planar caps required for current parity.
- Cone/frustum: one or two caps depending on end radii and bounded interval policy.
- Sphere/torus: no planar caps.

### Seam policy
- Cylinder: one explicit side seam plus cap circles.
- Cone/frustum: single revolve seam consistent with conical face loop expectations.
- Sphere: seam may be topologically implicit in current single-face no-loop representation; preserve that representation.
- Torus: two periodic seams (major/minor parameter directions), both required for current topology parity.

### Orientation/winding policy
- Preserve existing outward normal expectations from current face construction and loop handedness.
- AIR emitters should produce deterministic coedge order to maintain exporter determinism.

### Pole/singularity policy
- Sphere poles and cone apex (radius=0 end) require explicit admissibility/diagnostic clauses.
- Suggested diagnostics: non-finite radii/height, invalid major<=minor torus, degenerate sweep interval, unstable seam placement.

### Provenance/tolerance
- Preserve current numeric tolerances and parameter intervals (e.g., `0..2π` circles, `0..height` seams).
- Face naming/provenance should remain internal and deterministic; no SEM expansion in this milestone.

## 7) STEP/AP242 alignment

Primitive-as-AIR is aligned with AP242 analytic families already exercised by exporter/importer and tests:

- Box -> planar faces (`PLANE`).
- Cylinder -> `CYLINDRICAL_SURFACE` + planar caps.
- Cone/frustum -> `CONICAL_SURFACE` (+ optional planar caps).
- Sphere -> `SPHERICAL_SURFACE`.
- Torus -> `TOROIDAL_SURFACE`.
- Sweep semantics map naturally to `SURFACE_OF_LINEAR_EXTRUSION` / `SURFACE_OF_REVOLUTION` concepts at intent level, while final emitted topology can still use direct analytic surfaces as today.

Therefore AIR normalization can improve constructive-intent consistency without requiring any exporter rewrite.

## 8) Migration risk ranking

1. **Lowest risk: Box**
   - No periodic seams/poles; explicit planar topology is straightforward.
2. **Low–moderate: Cylinder**
   - Mostly stable, but seam/cap orientation must match existing behavior.
3. **Moderate: Cone/frustum**
   - Apex/radius-zero and conical interval semantics add edge cases.
4. **High: Sphere**
   - Pole singularities + single-face periodic representation sensitivity.
5. **Highest: Torus**
   - Double-periodic seam topology is easiest to perturb unintentionally.

## 9) Recommended next milestones

- **AIR-X4 (lab-only EVT): Box-as-AirExtrude evidence lane**
  - Deterministic mapping report + topology parity assertions only.
- **AIR-V3 (production): Box migration**
  - Constructor wrapper calls AIR emitter internally; parity tests gated.
- **AIR-V4 (production): Cylinder migration**
  - Add seam/cap parity tests before switching.
- **AIR-X5 (lab): Cone/frustum revolve policy matrix**
  - Focus on apex/cap admissibility diagnostics.
- **AIR-V5 (production): Cone/frustum migration**.
- **Sphere/Torus** remain direct constructors until dedicated seam/pole parity labs prove deterministic equivalence and no STEP/test regressions.

Guardrails per step:
- topology counts and loop/edge orientation checks,
- deterministic STEP export snapshots,
- targeted boolean smoke tests that currently consume primitive outputs,
- Firmament lowerer/export tests ensuring analytic surface family retention.

## 10) Explicit non-goals

- No production primitive rewrites in AIR-X3.
- No public API changes.
- No STEP exporter/importer changes.
- No boolean kernel changes.
- No NURBS/B-spline AIR core broadening.
- No fillet/chamfer/shell/general sketch-kernel expansion.

## 11) Confidence ratings

- **Representability claim**: High.
- **Box/cylinder risk ranking**: High.
- **Cone/frustum policy recommendation**: Medium-high.
- **Sphere/torus high-risk assessment**: High.
- **Migration ladder practicality**: Medium-high (depends on future parity-test depth).

Overall confidence: **High** for architecture direction, **Medium-high** for exact migration sequencing.
