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
