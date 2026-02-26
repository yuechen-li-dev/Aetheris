# Milestones

## M00 — Project Charter + Repo Skeleton Hardening

- [x] Add kernel core and kernel test scaffolding projects.
- [x] Establish project docs for vision, non-goals, architecture, and numerics policy.
- [x] Set contribution/editor guardrails.
- [x] Ensure CI restores, builds, and tests .NET projects.

## M01 — Diagnostics and Result Model Baseline

- [x] Define a kernel operation result envelope (`KernelResult<T>`) for success/failure with diagnostics.
- [x] Introduce shared kernel diagnostic primitives (severity + stable codes + immutable payload).
- [x] Enforce guardrails for success/failure diagnostic shape (no success errors, failure has at least one error).
- [x] Add focused unit tests that lock result/diagnostic semantics.

## M02 — Tolerance and Numerics Primitives

- [ ] Introduce centralized tolerance primitives and policy-backed defaults.
- [ ] Add foundational numeric utility types required for later geometric operations.
- [ ] Add focused tests that enforce tolerance policy behavior.


## M03 — Core Math Primitives + Affine Transforms

- [x] Add immutable 3D primitives (`Point3D`, `Vector3D`, `Direction3D`) with explicit semantics and normalization checks.
- [x] Add minimal affine `Transform3D` wrapper with point/vector/direction application, composition, and inverse behavior.
- [x] Add minimal spatial substrate (`BoundingBox3D`, `Ray3D`) for later query/intersection work.
- [x] Add focused unit tests for primitive math invariants and transform behavior.

## M04 — Topology Entity IDs + Minimal B-rep Topology Graph Skeleton

- [x] Add strongly typed topology IDs (`BodyId`, `ShellId`, `FaceId`, `LoopId`, `CoedgeId`, `EdgeId`, `VertexId`) for stable in-memory references.
- [x] Add topology-only entity graph primitives (body/shell/face/loop/coedge/edge/vertex) with ID-based references and no geometry binding.
- [x] Add a minimal topology model container and a small builder helper for readable test graph assembly.
- [x] Add reference-integrity validator v0 (dangling references and local loop/coedge consistency only).
- [x] Add unit tests, including a cube-like topology graph skeleton validation test.
- [x] Explicitly defer geometry binding and manifold-level correctness checks to future milestones.


## M05 — Analytic Geometry Primitives v1 (No Topology Binding)

- [x] Add minimal analytic geometry parameter helpers for stable parameter-domain usage (`ParameterInterval`).
- [x] Add exact analytic curve primitives (`Line3Curve`, `Circle3Curve`) with explicit parameterization conventions and evaluation APIs.
- [x] Add exact analytic surface primitives (`PlaneSurface`, `CylinderSurface`, `SphereSurface`, `ConeSurface`) with constructor validation and documented parameter conventions.
- [x] Add focused geometry unit tests that lock evaluation behavior/orientation conventions and invalid-construction guardrails.
- [x] Explicitly defer topology-geometry binding to M06.

## M06 — Topology–Geometry Binding v1 (Minimal B-rep Body Definition)

- [x] Add strongly typed geometry IDs and a lightweight geometry store for reusable curve/surface definitions.
- [x] Add explicit topology-to-geometry bindings (`EdgeId -> CurveGeometryId`, `FaceId -> SurfaceGeometryId`) with optional edge parameter intervals.
- [x] Add a minimal aggregate B-rep body model that combines topology, geometry, and bindings.
- [x] Add binding validator v0 for topology + binding reference integrity (not advanced geometric/manifold correctness).
- [x] Add focused tests, including manual construction and validation of a simple box-like B-rep fixture.
- [x] Explicitly defer full geometric consistency checks, trim-loop/p-curve support, and modeling operations.
