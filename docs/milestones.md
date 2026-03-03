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
- [x] Explicitly defer general B-rep/B-rep intersection, fragment splitting/classification, and robust rebuild/healing to later milestones.


## M13 — Boolean Ops v1 (Real but Narrow, Axis-Aligned Box/Box Subset)

- [x] Extend the staged boolean pipeline with real support for recognized axis-aligned box vs axis-aligned box inputs (from `BrepPrimitives.CreateBox(...)`).
- [x] Support `Intersect` only for positive-volume overlap (single-box result); disjoint and touching-only contact return deterministic `NotImplemented` diagnostics because empty/non-solid results are not representable in M13.
- [x] Support `Union` for containing cases and exact single-box outcomes only; disjoint unions and non-single-box unions (for example L-shaped overlap unions) return deterministic `NotImplemented`.
- [x] Support `Subtract` for no-overlap passthrough, full-removal empty-result diagnostics, and exact single-box clip outcomes; non-single-box subtraction remains `NotImplemented`.
- [x] Rebuild supported outputs via `BrepPrimitives.CreateBox(...)` plus placement translation and validate results with strict `BrepBindingValidator`.
- [x] Explicitly defer rotated boxes, non-box primitives, multi-body/disjoint boolean outputs, arbitrary B-rep intersection/splitting, and general boolean robustness.

## M14 — Display Tessellation Engine v1 (Not Simulation Meshing)

- [x] Add display-focused tessellation DTOs and options (`DisplayTessellationResult`, face patches, edge polylines) with topology backreferences for picking/highlighting workflows.
- [x] Add `BrepDisplayTessellator.Tessellate(...)` returning `KernelResult<DisplayTessellationResult>` and deterministic face/edge iteration by topology ID order.
- [x] Support display tessellation for the current analytic subset: planar faces (single all-line loop or single circular loop), cylinder/cone side faces in the current seam-loop layout, and untrimmed sphere faces.
- [x] Support edge tessellation for `Line3Curve` (2-point polyline) and `Circle3Curve` (deterministic sampled closed polyline).
- [x] Return structured `NotImplemented` diagnostics for unsupported trims/layouts; fail the full tessellation result on the first unsupported bound face/edge case.
- [x] Explicitly defer general trimmed-surface tessellation (including arbitrary holes/multi-loop trims), NURBS/spline surfaces, volumetric/analysis meshing, and renderer/GPU integration.

## M15 — Picking Data + Selection Query Contracts v1

- [x] Add explicit picking/selection contracts under `Brep.Picking` (`SelectionEntityKind`, `PickHit`, `PickQueryOptions`) with topology references (`FaceId`, `EdgeId`, optional `BodyId`) and viewer-friendly hit payloads (`t`, point, optional normal).
- [x] Add `BrepPicker.Pick(...)` entry points that work against display tessellation (either precomputed or generated on demand) and return `KernelResult<IReadOnlyList<PickHit>>`.
- [x] Support tessellation-driven face picking (ray/triangle) and edge picking (ray/segment closest approach with tolerance), including deterministic hit sorting.
- [x] Lock deterministic policy: sort by ascending `t`; when `|Δt| <= SortTieTolerance`, edge hits are ordered before face hits; `NearestOnly` returns the first hit from that deterministic ordering.
- [x] Default backface handling is culling (`IncludeBackfaces = false`), with opt-in backface inclusion.
- [x] Add focused unit tests for face hits/misses, edge tolerance behavior, face-vs-edge precedence, nearest-only determinism, and structured diagnostics for malformed input.
- [x] Explicitly defer UI/backend integration, interaction tools, acceleration structures, and caching/performance optimizations.

## M16 — ASP.NET Backend Kernel Host v1 (Local API)

- [x] Add a minimal in-memory document store for local backend kernel sessions (volatile, process-lifetime only).
- [x] Introduce explicit backend DTO contracts for document, modeling, tessellation, and picking endpoints without exposing raw kernel internals as API contracts.
- [x] Expose local HTTP endpoints for document lifecycle, primitive creation (box/cylinder/sphere), extrude/revolve subset operations, narrow M13 boolean operations, display tessellation, and picking.
- [x] Add deterministic kernel diagnostic-to-HTTP mapping with structured error payloads for invalid, unsupported, validation, and missing-resource paths.
- [x] Add focused integration tests for core happy paths and deterministic error behavior.
- [x] Defer frontend integration details and API versioning to M17+.

## M17 — Web Protocol Stabilization + Versioned Contracts v1

- [x] Stabilize the canonical backend API surface under `/api/v1/documents/...` for frontend integration.
- [x] Adopt a consistent HTTP response envelope for all routes: `success`, `data`, `diagnostics`.
- [x] Normalize diagnostics payload shape (`code`, `severity`, `message`, `source`) for all errors, including not-found responses.
- [x] Preserve M16 capability coverage (document summary/create, primitives, extrude/revolve/boolean, tessellate, pick) while standardizing protocol shape only.
- [x] Keep temporary unversioned compatibility aliases under `/api/documents/...` during migration; v1 routes are canonical.
- [x] Add integration tests that lock versioned route behavior, envelope shape, diagnostics determinism, and compatibility alias behavior.
- [x] M18 frontend integration targets stable v1 contracts and envelope semantics.

## M18 — React Viewer Shell v1 (Viewport + Scene Display)

- [x] Add a minimal React viewer shell in `aetheris.client` that targets canonical `/api/v1/documents/...` routes.
- [x] Add explicit TypeScript API contracts/envelope handling for document create/summary, box creation, and tessellation requests.
- [x] Add a basic 3D viewport rendering tessellated face patches and edge polylines with orbit/zoom/pan camera controls.
- [x] Add a simple debug/status panel showing document/body state, request status, and surfaced diagnostics.
- [x] Keep scope intentionally narrow: viewer/debug shell + primitive creation integration only.
- [x] Defer rich modeling workflows, selection UX, editing manipulators/gizmos, undo/redo, and persisted documents.

## M18.5 — Viewer Picking Integration Debug Slice

- [x] Extend the React viewer API client with typed `/api/v1/documents/{documentId}/bodies/{bodyId}/pick` support using the M17 response envelope.
- [x] Add viewport click-to-ray mapping and nearest-only pick requests against the backend endpoint (backend semantics remain source of truth).
- [x] Surface pick status, hit details (kind, IDs, t, point, normal), and diagnostics/no-hit feedback in the debug panel.
- [x] Add minimal debug highlighting for nearest picked face/edge using tessellation topology IDs.
- [x] Defer full selection UX/tooling architecture (multi-select, inspectors, manipulators, hover/tool systems) to M19+.

## M19 — Basic Modeling UI v1 (Primitive + Transform)

- [x] Extend the React frontend with explicit modeling controls: box primitive creation form, body list, active body selection, and numeric translation inputs.
- [x] Keep backend as source of truth by refreshing document summary + tessellation after create/transform operations and surfacing deterministic status/diagnostics.
- [x] Add minimal backend transform route (`POST /api/v1/documents/{documentId}/bodies/{bodyId}/transform`) with typed DTOs and M17 envelope semantics.
- [x] Apply document-level body transforms to tessellation and picking responses so viewport updates and hit data stay in world space after translation.
- [x] Preserve M18/M18.5 debug visibility (active body, body count, pick status/hits/diagnostics, tessellation counts) while deferring booleans UI, gizmos, undo/redo, feature tree, and persistence to later milestones.


## M20 — Boolean UI Workflow v1 (Two-Body Union/Subtract/Intersect)

- [x] Add explicit React boolean controls for target body, tool body, operation (`Union`, `Subtract`, `Intersect`), and execute action in the modeling panel.
- [x] Integrate `/api/v1/documents/{documentId}/operations/boolean` through typed client contracts and M17 envelope parsing (`parseEnvelope<T>` with deterministic `ApiError` diagnostics).
- [x] Keep backend as source of truth for support limits: M13 narrow axis-aligned box/box subset is unchanged, and unsupported cases surface structured diagnostics (including `NotImplemented`) directly in the UI.
- [x] Adopt deterministic success semantics aligned with current backend behavior: keep original bodies and add a new boolean result body, refresh document summary, auto-select/tessellate the returned result body, and intentionally clear pick highlight/hits after success.
- [x] Preserve failure stability: on unsupported/error responses the previous viewport/tessellation state is preserved while status/diagnostics update for observability.
- [x] Defer boolean previews, feature history, undo/redo, gizmo-based selection, advanced naming/metadata workflows, and generalized boolean support beyond M13.

## M21 — Hardening Pass 1 (Regression Corpus + Crash Resistance)

- [x] Add a curated regression corpus for representative kernel/backend flows (primitives, extrude/revolve subsets, M13 boolean support boundaries, tessellation/picking contracts, and versioned envelope behavior).
- [x] Introduce deterministic hardening fixtures for repeatable scenarios (canonical box-pair booleans, extrude/revolve profiles, and pick rays).
- [x] Add bounded randomized stress checks with fixed seeds for stable areas (box construction, boolean classification behavior, and pick no-throw/order stability).
- [x] Harden routine failure paths to remain diagnostic-first and no-throw for malformed/unsupported requests.
- [x] Keep scope hardening-only: no new modeling capabilities or UI feature expansion.
- [x] Defer known fragilities (general booleans beyond axis-aligned single-box results, broad trimmed-surface tessellation fidelity, and advanced pick acceleration/performance work).

## M22 — AP242 Mapping Layer Skeleton + Export v1 (Subset Only)

- [x] Add a focused AP242 export mapping layer in the kernel (`Aetheris.Kernel.Core.Step242`) with a deterministic text writer and a single export entry point.
- [x] Keep AP242 as a mapped serialization layer: the kernel B-rep model remains authoritative and unchanged in-memory.
- [x] Export a narrow, explicit subset: single-body/single-shell solids with planar faces, line edges, and loop-based topology (box primitive path).
- [x] Return structured `NotImplemented` diagnostics for unsupported export cases (for example: periodic/loopless sphere face, non-planar surfaces, non-line edge curves, multi-body layouts).
- [x] Add regression tests for successful box export structure, deterministic output stability, and unsupported-case no-throw diagnostics.
- [x] Defer AP242 import work to M23 and broader schema/entity coverage to later milestones.

## M23 — AP242 Import v1 (Subset Only, Export-Roundtrip-Oriented)

- [x] Add a subset-only AP242 import entry point (`Step242Importer.ImportBody(string)`) that returns `KernelResult<BrepBody>` and operates on STEP text (no file I/O).
- [x] Add a minimal STEP text parser/AST for M22-compatible entity assignment syntax (`#n=ENTITY(...);`), references, strings, numbers, logicals, and lists.
- [x] Decode and map the M22 export subset (line edges + planar faces with loop/shell topology) into kernel topology, geometry, and bindings.
- [x] Run `BrepBindingValidator` as the final import acceptance gate; return structured diagnostics on parse/import/validation failures.
- [x] Add importer-focused tests for success, M22 export→import round-trip (box subset), and malformed/unsupported no-throw behavior.
- [x] Explicitly keep support narrow: arbitrary third-party STEP/AP242 files are expected to fail with diagnostics; broader schema/entity coverage is deferred.
- [x] Defer import scope expansion and broader round-trip reliability hardening to M24.


## M24 — AP242 Round-Trip Reliability v1 (Subset Stability)

- [x] Add subset-focused reliability coverage for repeated AP242 cycles (`export -> import -> export`) on the existing box-like planar/line topology scope.
- [x] Lock an explicit round-trip equivalence strategy: topology count invariants + geometry kind invariants (line edges / planar faces) + tessellation/picking smoke checks.
- [x] Confirm deterministic STEP text behavior for the supported subset: M22 canonical export is byte-stable from the first cycle and remains stable across subsequent cycles.
- [x] Add a minimal generated golden/fixture corpus for supported subset and importer failure-path diagnostics (malformed input, unsupported entity, broken reference).
- [x] Keep AP242 support scope intentionally narrow (single-body/single-shell solids with planar faces and line edges); broader entity/schema coverage remains deferred.

## M25 — Assembly/Document Model v1 (Lightweight and AP242-Aligned)

- [x] Split document state into body definitions (shared `BrepBody`) and body occurrences (instance identity + per-occurrence placement).
- [x] Preserve existing route shapes while reinterpreting `bodyId` as occurrence identity for tessellation/picking/transform and document summary flows.
- [x] Add lightweight occurrence creation (`POST /api/v1/documents/{documentId}/occurrences`) so multiple occurrences can reference one shared definition.
- [x] Extend pick/document contracts minimally with occurrence identity and occurrence summaries while preserving M17 envelope behavior.
- [x] Keep React modeling UI compatible by listing/selecting occurrences and surfacing occurrence definition/translation context.
- [x] Add focused occurrence semantics + placement regression coverage; defer full assembly constraints, mates/kinematics, and broad AP242 assembly IO.


## M26 — STEP Document I/O Hooks v1 (In-Memory, Subset Only)

- [x] Add canonical backend STEP I/O endpoints for definition export and document import using the existing AP242 subset (`/api/v1/documents/{documentId}/definitions/{definitionId}/export/step`, `/api/v1/documents/{documentId}/import/step`).
- [x] Add minimal DTO contracts for STEP import/export request/response payloads, preserving M17 envelope and diagnostic mapping behavior.
- [x] Wire STEP import to create a new definition and a new identity-placed occurrence in in-memory document sessions (no DB/filesystem persistence).
- [x] Extend server integration coverage for export→import→tessellate success and required negative paths (empty payload, missing definition, malformed STEP diagnostics).
- [x] Add client API methods and explicit UI controls for button-driven STEP export/import text workflows, with imported occurrence auto-selection and tessellation refresh.
- [x] Extend client envelope tests for STEP API success parsing and failure diagnostic propagation through `ApiError`.
- [x] Defer AP242 assembly/product-structure mapping, multi-definition export, metadata completeness, and file-dialog UX to later milestones.

## M27 — Canonical STEP Hash Infrastructure

- [x] Compute deterministic canonical SHA256 hashes in backend STEP export responses using UTF-8 bytes of the exporter output text.
- [x] Extend STEP export API contracts to include a backend-authoritative `canonicalHash` field.
- [x] Add server integration coverage for determinism across repeated exports, import→export stability, and geometry-sensitive hash changes.
- [x] Update client STEP API parsing and inspector UI display to surface `canonicalHash` with copy-friendly monospace formatting.
- [x] Extend client API tests to validate `canonicalHash` parsing and maintain `ApiError` propagation behavior.

## M28 — STEP 242 File Upload Flow (Frontend)

- [x] Replace manual STEP paste import emphasis with a single-file upload flow using `.step/.stp` file input.
- [x] Reuse existing document import API path (`POST /api/v1/documents/{documentId}/import/step`) with backend-authoritative parsing and diagnostics.
- [x] On successful import, auto-refresh summary, activate imported occurrence, tessellate it, and immediately refresh canonical hash via definition export.
- [x] Add frontend safeguards for empty/oversized files and display diagnostics without silent error swallowing.
- [x] Add frontend tests for file-selection state updates, import payload wiring, canonical-hash refresh trigger, and `ApiError` propagation.
