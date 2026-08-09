# AETHERIS-CONTINUUM-M0 extraction report

## Outcome

`Aetheris.Continuum` is the architectural home for Continuum Implicit Representation, SDF evaluation, regular lattice classification, Cut cells, and geometry sampling. `Aetheris.Kernel.Core` has no project reference to Continuum and no longer contains its historical `Cir` folder.

## Assembly and folder structure

```text
Aetheris.Continuum/
  Cir/                 occupancy contracts and optional capabilities
  Regions/Analytic/    exact bounded M0 fixtures
  Backends/Sdf/        preserved node, composition, tape, interval, and volume code
  Lattice/             uniform Cartesian indexing, cells, classification, CutCell
  Sampling/            deterministic geometry-only sample plans
  Boundaries/          source identity and BoundaryOffsetMap seam
  Diagnostics/         deterministic JSON inspection
  Experiments/         reproducible M0 fixtures and metrics
  Bridges/Air/         quarantined historical AIR adapter
  Mirrors/             quarantined historical mirror admission/provenance

Aetheris.Continuum.Tests/
  Backends/Sdf/        moved regression suite
  Bridges/Air/         moved adapter regression suite
  Lattice/             CIR contracts, grid, fixtures, convergence, diagnostics
```

## Dependency graph

Before:

```text
Firmament / FrictionLab / CLI
              |
              v
     Aetheris.Kernel.Core
       - math and BRep
       - AIR
       - CIR/SDF runtime
       - adaptive continuum experiments
       - CIR mirror policy
```

After:

```text
CLI -> Firmament -> Aetheris.Continuum -> Aetheris.Kernel.Core
          |                 ^                     ^
          +-----------------+                     |
FrictionLab --------------------------------------+

Aetheris.Kernel.Core -X-> Aetheris.Continuum
```

Consumer assemblies that directly compile against CIR/SDF add an explicit Continuum reference. Core remains the lower-level dependency and grants a narrow friend-assembly relationship so the relocated legacy AIR bridge can consume existing internal AIR/prismatic records without broadening their public API in M0.

## CIR API

`IContinuumRegion` provides `Id`, `Bounds`, point `Classify`, and `Contains`. It does not require a scalar field. Optional interfaces are:

- `ISignedDistanceCapability`;
- `IGradientCapability`;
- `IBoundaryProjectionCapability`;
- `IMaterialRegionCapability`;
- `IBoundsClassificationCapability` for conservative/exact cell classification;
- `IBoundaryReferenceCapability` for attaching analytic, semantic, or future exact-BRep candidates.

`RegionId` and `MaterialRegion` are intentionally minimal. There is no material-property database.

## SDF backend extraction

The existing analytic primitives (box, cylinder, cone, sphere, torus), CSG-style field nodes (union, subtraction, intersection), transforms, point evaluation, tape lowering/runtime, interval evaluation, regular/adaptive volume estimators, and JudgmentEngine region planner moved from `Aetheris.Kernel.Core.Cir` to `Aetheris.Continuum.Backends.Sdf` without an engine rewrite.

`SdfContinuumRegion` is the adapter from the historical node/tape model to CIR occupancy plus optional signed-distance, gradient, and bounds-classification capabilities. This makes the direction explicit: the SDF backend implements CIR; CIR does not inherit an SDF requirement.

The historical public type names (`CirNode`, `CirTape`, and related records) remain inside the SDF backend for source migration economy. There is no old-namespace shim and no second CIR API. A later narrow rename is documented as debt.

## Core cleanup and ownership

Before extraction, `Aetheris.Kernel.Core/Cir` contained 8 files / 1,893 lines, and `Core/Air/AirCirMirrorAdapter.cs` added another consumer-specific implicit bridge.

Moved/extracted:

- 6 SDF runtime/planning files to `Backends/Sdf`;
- 2 mirror policy/convex mirror files to `Mirrors`;
- 1 AIR adapter to `Bridges/Air`;
- 12 CIR/SDF regression test files and 1 AIR bridge test to `Aetheris.Continuum.Tests`;
- all consuming namespaces from `Aetheris.Kernel.Core.Cir*` to explicit Continuum namespaces.

Remaining Core dependency surface consists of general `Point3D`, `Vector3D`, `Transform3D`, `BoundingBox3D`, tolerances, JudgmentEngine, and internal AIR/prismatic source records used through the declared friend assembly. Core has no Continuum project reference and no `Core/Cir` implementation files.

## Lattice and classification

`LatticeSpec` is a fixed 3D Cartesian box with `(CountX, CountY, CountZ)`, constant cell size, deterministic `k/j/i` enumeration, and row-major flattening `((k * Ny) + j) * Nx + i`. `CellIndex`, `CellBounds`, and `CellCenter` are value-oriented and contain no solver state.

`ContinuumGridClassifier` uses JudgmentEngine to choose between:

1. a backend/analytic `IBoundsClassificationCapability`, preferred for bounded robust classification;
2. a conservative bounds fallback, which returns Outside only for disjoint bounds and Cut otherwise.

Analytic box, plane, and cylindrical-hole fixtures classify bounds from exact inequalities. Classification never relies on the cell center alone. The SDF adapter uses the preserved interval evaluator plus a center-side tie-break only for zero-measure boundary contact.

`CutCell` contains only cell index/bounds, continuum region identity, boundary references, a `GeometrySamplePlan`, and occupancy estimate. It contains no displacement, stress, strain, matrix, timestep, or quadrature state.

## Geometry sampling

M0 uses deterministic subcell-center patterns with a configurable samples-per-axis value. Boundary samples contribute half measure to remove a directional bias when a deterministic sample lies exactly on an analytic interface. The plan records every sample position and classification, boundary candidates, sample count, and coverage.

Sampling is invoked only for Cut cells. A future selective 2x2x2/4x4x4 or hierarchical policy can reuse the same plan shape. No solver quadrature API or implementation exists.

## Boundary identity and offset-map seam

`BoundaryReference` can carry source representation/id, optional exact BRep face id, and semantic region. M0 analytic fixtures attach stable analytic boundary ids. No BRep correspondence is inferred.

`IBoundaryOffsetMap` sketches a source reference, local frame, offset/normal samples, and approximation metadata. It is a derived cache seam for M1, never a replacement authority.

## Fixture evidence

All values below come from the production `ContinuumM0Experiments` path and deterministic 4x4x4 geometry sampling for cylindrical-hole Cut cells.

| Fixture | Resolution | Cells | Inside | Outside | Cut | Geometry samples | Exact volume | Estimated volume | Relative error |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| aligned box | 4x4x4 | 64 | 8 | 56 | 0 | 0 | 8 | 8 | 0 |
| oblique plane | 8x8x8 | 512 | 224 | 224 | 64 | 512 | 4 | 4 | 0 |
| cylindrical hole, coarse | 8x8x2 | 128 | 96 | 8 | 24 | 1,536 | 12.8584073464 | 12.75 | 0.843085% |
| cylindrical hole, medium | 16x16x4 | 1,024 | 784 | 128 | 112 | 7,168 | 12.8584073464 | 12.828125 | 0.235506% |
| cylindrical hole, fine | 32x32x8 | 8,192 | 6,400 | 1,312 | 480 | 30,720 | 12.8584073464 | 12.84765625 | 0.083611% |

The box boundary is aligned to grid planes, so zero-measure contact does not create Cut cells. The oblique plane cuts exactly one diagonal cell band and symmetric deterministic samples recover half the support-box volume. The hole fixture retains a boring regular lattice: 6,400 of 8,192 fine cells are trivially Inside and only 480 are sampled as Cut.

Exact boundary areas are 24 for the box and 48 for the block-with-hole fixture. M0 deliberately does not claim a sampled boundary-area estimator; that metric remains null rather than presenting an occupancy-derived guess as boundary geometry.

## Diagnostics, determinism, and performance

`ContinuumGridDiagnostics.ToJson` emits lattice dimensions/cell size, aggregate counts, every cell state and occupancy, Cut indices, and boundary references with string enums and deterministic enumeration. A compact checked-in summary is at `docs/continuum/artifacts/m0-summary.json`.

The full 8,192-cell fine fixture plus 30,720 boundary samples averaged 13.01 ms over 20 warmed runs on the validation machine. All patterns, cell ordering, strategy tie-breaking, boundary ids, and JSON order are deterministic; a regression test compares repeated JSON byte-for-byte.

## Compatibility

No compatibility shim was added. All repository consumers were moved to the new namespaces in the same change, avoiding two permanent CIR APIs. Public downstream users of the pre-M0 namespace will need a source namespace update; this is preferred over preserving the incorrect Core ownership indefinitely.

## SurfaceMeshIR and BRep bridges

SurfaceMeshIR is not wired into M0 classification. It may later accelerate candidate-cell discovery and provide structured boundary hints, but occupancy must still be confirmed by CIR. A generic BRep-to-CIR converter remains deliberately absent; existing bounded Firmament/AIR mirrors continue as explicit consumers/bridges.

## Recommended M1

Run one cylindrical-hole boundary experiment that compares fixed 2x2x2, fixed 4x4x4, and selective/hierarchically reused sampling in the same Cut cells, then attach an analytic `IBoundaryOffsetMap` for the cylindrical wall. Measure occupied-volume error, local boundary-position/normal error, reused sample count, and cache size while keeping the lattice fixed. Do not add AMR or solver quadrature in that experiment.

## Validation

- `dotnet restore Aetheris.slnx`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed with 0 warnings and 0 errors.
- full solution tests: 2,484 passed (Core 931, Firmament 1,061, CLI 350, Server/Cadmata 49, Continuum 93); the opt-in legacy FrictionLab assembly reported no discoverable tests under the default gate.
- TSPack from `aetheris.client`: sync, check, format check, typecheck, 69 frontend tests, production build, and lint passed. The existing lock audit still reports 21 multi-version packages and 114 acknowledged/blocked lifecycle scripts.
- `git diff --check`: passed.
