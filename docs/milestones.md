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

## M07 — B-rep Query/Traversal Helpers v1 (Read-Only)

- [x] Add read-only traversal/query helpers over B-rep topology hierarchy (body/shell/face/loop/coedge/edge/vertex).
- [x] Add explicit edge/face geometry-binding resolution helpers for curve/surface access.
- [x] Add minimal convenience queries for common traversal needs with explicit duplicate-edge semantics.
- [x] Add focused unit tests for happy-path traversal, duplicate behavior, and missing-reference handling.
- [x] Explicitly defer modeling operations, tessellation, and advanced validation checks.

## M08 — Primitive Solid Constructors v1 (Box / Cylinder / Sphere)

- [x] Add an explicit primitive B-rep API (`BrepPrimitives`) with `CreateBox`, `CreateCylinder`, and `CreateSphere` constructors returning `KernelResult<BrepBody>`.
- [x] Construct minimal topology + analytic geometry + bindings for axis-aligned box, closed right circular cylinder, and closed sphere representations.
- [x] Validate constructor outputs via `BrepBindingValidator` in strict completeness mode before returning success.
- [x] Add focused unit tests for valid creation, invalid parameter diagnostics, expected topology/geometry binding types, and traversal safety.
- [x] Document M08 simplifications: cylinder uses a single explicit seam edge on the side face; sphere uses a single closed periodic face with no boundary loops.

## M09 — Spatial Queries v1 (Ray Hits + Point Classification)

- [x] Add a minimal spatial query API over `BrepBody` for ray hits (`Raycast`) and point containment (`ClassifyPoint`).
- [x] Add explicit query value/result types (`PointContainment`, `RayHit`, optional `RayQueryOptions`) under `Brep.Queries`.
- [x] Support M08 primitive outputs (`CreateBox`, `CreateCylinder`, `CreateSphere`) with deterministic hit ordering and tolerance-aware boundary handling.
- [x] Add focused unit tests for primitive hit/classification behavior, tangent handling, seam dedup behavior, and unsupported-body fallbacks.
- [x] Explicitly defer arbitrary trimmed/general B-rep intersection and classification support.

## M10 — Programmatic Extrude Framework v1 (No Sketcher)

- [x] Add a minimal programmatic profile model for extrusion input using local 2D planar coordinates (single outer closed loop, line segments only).
- [x] M10 profile validation intentionally remains basic and not a full polygon-simplicity/self-intersection solver; some invalid polygons are rejected by existing degeneracy checks, but robust simple-polygon validation is deferred.
- [x] Add an explicit extrusion API (`BrepExtrude.Create`) returning `KernelResult<BrepBody>` with strict validator-backed output checks.
- [x] Support constant-depth linear extrusion along +normal of an explicit frame, producing top/bottom caps and planar side faces.
- [x] Add focused unit tests for profile/extrude validation, topology and binding expectations, traversal safety, depth-direction convention, and winding behavior.
- [x] Explicitly defer sketching/constraints, holes/multi-loop profiles, arcs/splines, draft/thin features, booleans, and generalized feature history.

## M11 — Programmatic Revolve Framework v1 (No Sketcher)

- [x] Add a minimal programmatic revolve API (`BrepRevolve.Create`) returning `KernelResult<BrepBody>` with validator-backed outputs.
- [x] Reuse M10 profile/frame primitives where practical (`ProfilePoint2D` + `ExtrudeFrame3D`) with an explicit world-space revolve axis (`RevolveAxis3D`).
- [x] M11 supported subset is intentionally narrow: exactly one two-point line-segment profile with positive radii at both endpoints and full-turn (`2*pi`) angle only.
- [x] Build closed solids for that subset with analytic side surfaces (`CylinderSurface` for constant radius, `ConeSurface` for varying radius) plus planar end caps.
- [x] Seam strategy: periodic side faces use one explicit seam edge referenced twice in the side loop; cap boundaries are represented as closed circular edges.
- [x] Add focused tests for validation/diagnostics, full-angle semantics, seam behavior, topology/geometry expectations, traversal safety, and strict binding validation.
- [x] Explicitly defer partial revolve, axis-touching/axis-crossing profiles, generalized polyline/curve revolves, and self-intersection handling.

## M12 — Boolean Infrastructure v1 (Pipeline Scaffolding, Not Full Booleans)

- [x] Add explicit boolean operation contracts (`BooleanOperation`, `BooleanRequest`) and a shared `BrepBoolean.Execute` entry point returning `KernelResult<BrepBody>`.
- [x] Introduce staged boolean pipeline scaffolding (`ValidateInputs`, `AnalyzeInputs`, `ComputeIntersections`, `ClassifyFragments`, `RebuildResult`, `ValidateOutput`) plus minimal intermediate data types for stage handoff.
- [x] Support tiny end-to-end identity shortcuts for same-instance `Union` and `Intersect`; outputs are validated in strict mode via `BrepBindingValidator` before success is returned.
- [x] Return deterministic structured `NotImplemented` diagnostics for all non-supported M12 boolean requests, including same-instance `Subtract` (empty-body representation deferred).
- [x] Add focused unit tests locking supported shortcuts, unsupported behavior, deterministic diagnostics, and no-throw behavior for routine unsupported cases.
- [x] Explicitly defer general B-rep/B-rep intersection, fragment splitting/classification, and robust rebuild/healing to M13.
