# DISPLAY-ARCH-A0 — Display/materialization architecture audit

## 1. Executive summary

Aetheris currently has two display stories that are not yet cleanly separated:

1. an **analytic display packet** emitted by the server for a bounded subset of BRep faces; and
2. a **legacy tessellation lane** that remains the API fallback whenever analytic coverage is incomplete.

That means display authority is ambiguous. BRep is still the geometry/topology authority, but the viewer-visible artifact is either client-generated triangle meshes from analytic DTOs or backend-generated triangle meshes from `BrepDisplayTessellator`. In both cases Three.js ultimately renders `BufferGeometry` triangles, not exact analytic surfaces. The current “analytic display” path is therefore a better data contract than the old mesh-only endpoint, but it is not yet a true analytic viewer: exactness is lost when the client converts analytic DTOs into fixed-resolution triangle patches.

FTC-07 exposed this as an architecture problem rather than an AP242 importer problem. Existing evidence shows FTC-07 imports and canonicalizes successfully; the failure occurs after import when `display/prepare` attempts view materialization. The bounded diagnostic identifies `Viewer.Tessellation.Timeout` on face 9, surface `Plane`, phase `PlanarTriangulationWithHoles`. The server display path still invokes tessellation when the analytic packet is not complete, so a pathological planar-with-holes display face can fail the entire display preparation request even though STEP parsing, BRep construction, and export are viable.

The immediate problem is not “make tessellation more accurate.” The problem is that tessellation is still acting as an implicit panic path for display completeness. Accuracy-driven tessellation is inherently expensive, and the current code contains specialized, accumulated support for planes, cylinders, cones, spheres, tori, B-spline scaffolds, trim-loop heuristics, loop classification, and many geometric retries. Treating that system as a universal fallback recreates exactly the failure mode that FTC-07 revealed.

The recommended architecture is:

```text
BRep remains geometry/topology authority.
DisplayIR/ViewIR becomes the server-emitted view authority.
Analytic DTOs become one DisplayIR lane.
Tessellation becomes an explicit, bounded DisplayIR lowering lane, not fallback authority.
CIR display remains future/experimental until a concrete preview evaluator/renderer exists.
Three.js consumes DisplayIR primitives, proxies, wireframes, diagnostics, and optional meshes; it does not own geometric truth.
```

Short term, FTC-07 should be addressed by adding bounded partial display behavior first: unsupported or timed-out faces should degrade to wireframe/diagnostic/bounding proxies rather than fail the whole import/display flow. Then the planar-with-holes lane can be rewritten or quarantined behind explicit quality/diagnostic controls.

## 2. Current display pipeline map

### STEP import to BRep body

- `Aetheris.Server/Api/KernelEndpoints.cs` maps `POST /api/v1/documents/{documentId}/import/step` and calls `Step242Importer.ImportBody(request.StepText)`. On success, the imported `BrepBody` is added to the volatile document store as a definition plus occurrence.
- The CLI evidence path uses `Aetheris.CLI` commands such as `analyze` and `canon`; FTC-07-specific documentation records that these complete before view materialization.
- The imported representation is an explicit BRep body, not recovered AIR or CIR. Existing V2 docs also describe STEP as an interchange boundary and BRep as the explicit topology backend.

### Server document/body endpoints

- `KernelEndpoints.MapKernelApi` registers both `/api/v1/documents` and `/api/documents`.
- Primitive, operation, transform, import, export, tessellation, display preparation, and pick endpoints all live in `Aetheris.Server/Api/KernelEndpoints.cs`.
- `POST /bodies/{bodyId}/tessellate` directly calls `BrepDisplayTessellator.TessellateBounded` and returns `TessellationResponseDto`.
- `POST /bodies/{bodyId}/display/prepare` first builds an analytic packet, then decides whether to invoke fallback tessellation.

### Display/prepare endpoint

The actual `display/prepare` flow is:

```text
KernelEndpoints display/prepare
  -> AnalyticDisplayPacketBuilder.Build(body)
  -> ResolveDisplayLane(packet)
  -> if lane != analytic-only:
       DisplayPreparationFallbackBuilder.Build(body, options)
       -> BrepDisplayTessellator.TessellateBounded(...)
       -> optional B-spline UV scaffold substitution
  -> DisplayPreparationResponseDto(lane, analyticPacket, tessellationFallback?)
```

The lane logic is simple:

- `analytic-only`: all faces are supported by the analytic packet.
- `mixed-fallback`: some analytic faces and some fallback faces.
- `fallback-only`: no analytic faces are supported.

The important architecture issue is that `mixed-fallback` and `fallback-only` still require a successful backend mesh fallback for display preparation to succeed.

### Backend display preparation DTOs

Relevant DTOs:

- `DisplayPreparationResponseDto`: `Lane`, `AnalyticPacket`, optional `TessellationFallback`.
- `AnalyticDisplayPacketDto`: `BodyId`, `AnalyticFaces`, `FallbackFaces`.
- `AnalyticDisplayFaceDto`: face/shell ids, surface kind, loop count, optional domain hint, and one of plane/cylinder/cone/sphere/torus geometry DTOs.
- `FacePatchDto`: triangle mesh positions, normals, indices, source, scaffold rejection reason.
- `TessellationResponseDto`: face mesh patches and edge polylines.

### Backend analytic packet construction

- `AnalyticDisplayPacketBuilder.Build` walks shells/faces and calls `AnalyticDisplaySupportPolicy.TryGetSupportedSurface`.
- Supported analytic families are plane, sphere, cylinder, cone, and torus, with important trim restrictions.
- Planes can include an `OuterBoundary`; cylindrical and conical faces can include min/max V domain hints.
- Unsupported faces are listed as `AnalyticDisplayFallbackFaceEntry` with a reason such as missing geometry, unsupported surface kind, or unsupported trim.

### Backend tessellation/materialization

Main files:

- `Aetheris.Kernel.Core/Brep/Tessellation/BrepDisplayTessellator.cs`
- `Aetheris.Kernel.Core/Brep/Tessellation/TrimmedSurfaceTessellator.cs`
- `Aetheris.Kernel.Core/Brep/Tessellation/PlanarPolygonTriangulator.cs`
- `Aetheris.Kernel.Core/Brep/Tessellation/DisplayTessellationExecutionBudget.cs`
- `Aetheris.Kernel.Core/Brep/Tessellation/DisplayPreparationFallbackBuilder.cs`
- `Aetheris.Kernel.Core/Brep/Tessellation/BsplineUvGridScaffoldBuilder.cs`
- `Aetheris.Kernel.Core/Brep/Tessellation/UvTrimMaskExtractor.cs`

`BrepDisplayTessellator.TessellateBounded` wraps face and edge tessellation in a `DisplayTessellationExecutionBudget`. Face dispatch supports planes, cylinders, cones, spheres, tori, and B-spline surfaces. Multi-loop planar faces enter `PlanarTriangulationWithHoles`, the FTC-07 timeout phase.

`DisplayPreparationFallbackBuilder.Build` calls the bounded tessellator, then opportunistically replaces B-spline face patches with accepted UV-grid scaffold patches for a bounded subset. This is a fallback mesh builder, not a DisplayIR authority model.

### Frontend rendering path

The React app does not call the old tessellation endpoint in the normal display flow. It calls `prepareBodyDisplay`, stores `displayPreparation`, and sets `tessellation` to `preparedDisplay.tessellationFallback` for compatibility/status.

Actual frontend flow:

```text
App.tsx
  -> refreshSummaryAndActiveTessellation(...)
  -> prepareBodyDisplay(documentId, selected)
  -> buildDisplaySceneData(displayPreparation)
       -> analytic-only: mapAnalyticPacketToRenderData
       -> mixed-fallback: map analytic + filtered tessellation fallback
       -> fallback-only: map tessellation fallback
  -> AetherisViewport
       -> FaceMesh
       -> Three.js BufferGeometry + MeshStandardMaterial
```

`analyticMapper.ts` converts analytic DTOs into triangle patches in the browser. Planes are triangulated with a simple ear-clipping polygon routine over the outer boundary only. Cylinders, cones, spheres, and tori are sampled at fixed angular/latitudinal segment counts. `AetherisViewport.tsx` now owns the Three.js adapter layer (historically `ViewerViewport.tsx`) and creates Three.js `BufferGeometry` for every face and renders `<mesh geometry={geometry} material={material} />`.

## 3. Authority model

### What is currently authoritative for display?

There is no single clean display authority. Current effective authority is:

- **BRep**: authoritative for exact geometry/topology.
- **Analytic display packet**: authoritative for deciding which faces are analytically describable for the current display contract.
- **Tessellation fallback**: still authoritative for any display-complete response when analytic coverage is incomplete.
- **Frontend mappers**: authoritative for the final triangle density and actual visual shape for analytic faces.

### Is the backend sending analytic surfaces?

Yes. `display/prepare` sends analytic DTOs for planes, cylinders, cones, spheres, and tori when `AnalyticDisplaySupportPolicy` admits the face.

### Is the backend sending tessellated meshes?

Yes, but only when the display lane is not `analytic-only`. The fallback is a full `TessellationResponseDto` containing face patches and edge polylines.

### Is the frontend tessellating anything?

Yes. The frontend tessellates analytic DTOs into render triangles. `analyticMapper.ts` triangulates plane boundaries and samples cylinders/cones/spheres/tori into `RenderFacePatch` buffers.

### Is Three.js rendering analytic primitives or triangles?

Triangles. Three.js is used through `BufferGeometry`, `BufferAttribute`, and `MeshStandardMaterial`. No shader-level analytic surface evaluation, ray-surface intersection renderer, or implicit-field renderer is present in the normal viewer path.

### Where does exactness get lost?

Exactness is lost in two places:

1. backend tessellation fallback, where exact BRep faces are converted to triangle meshes; and
2. frontend analytic mapping, where exact analytic surface DTOs are sampled into fixed triangle grids.

The current analytic packet preserves more semantic information across the API boundary, but the visual renderer is still mesh-based.

## 4. Tessellation inventory

### Entry points and callers

- `BrepDisplayTessellator.Tessellate(body, options)` unbounded direct tessellation.
- `BrepDisplayTessellator.TessellateBounded(body, options, executionTimeout)` bounded display tessellation.
- Server `/bodies/{bodyId}/tessellate` calls `TessellateBounded` directly.
- Server `/display/prepare` calls `DisplayPreparationFallbackBuilder.Build`, which calls `TessellateBounded` whenever the analytic lane is incomplete.
- Pick requests also accept tessellation options, but picking now has analytic query support elsewhere; the display audit did not find Three.js owning pick truth.

### Supported surface types

`TessellateFace` dispatches:

- `Plane`
- `Cylinder`
- `Cone`
- `Sphere`
- `Torus`
- `BSplineSurfaceWithKnots`

Unsupported surface kinds return a not-implemented diagnostic.

### Planar triangulation and holes

Planar faces use `TessellatePlanarFace`:

- fetch loop ids;
- flatten each planar loop into 3D points;
- select a primary/outer loop;
- treat all other loops as holes;
- call `PlanarPolygonTriangulator.TryTriangulateWithHoles` inside the bounded phase `PlanarTriangulationWithHoles`.

This is the exact FTC-07 failure phase. It is a hot display lane because `display/prepare` invokes it when analytic coverage is incomplete and a complete fallback mesh is required.

### Curved surface path

Curved faces are handled through a mixture of specialized paths and legacy grid paths:

- cylinders and cones have newer trim-resolution paths and legacy fallbacks;
- spheres and tori have specialized trim parameter resolution and grid patch generation;
- B-spline surfaces use bounded grid/scaffold behavior;
- `TrimmedSurfaceTessellator` samples UV grids, classifies triangles against trim loops, and recursively refines boundary-straddling triangles to a bounded depth.

### Loop computation and classification debt

The tessellator contains many specialized if-ladders and recognizers for coedge counts, circle/B-spline mixtures, cone/revolved loops, sphere trims, cylinder spans, near-full-wrap bridges, and ambiguous shorter-span choices. This has value as accumulated compatibility work, but it is not a good universal fallback authority because it mixes geometry policy, display approximation, topology heuristics, and diagnostics in one large file.

Potentially expensive patterns include:

- per-face loop flattening and reclassification;
- primary-loop selection over flattened loop data;
- planar nested-loop/hole treatment through a triangulation-with-holes algorithm;
- UV grid sampling and per-triangle trim classification;
- boundary refinement that appends extra vertices for straddling triangles;
- point-classification caches with finite caps;
- many geometric retry/special-case branches for periodic surfaces and trim ambiguity.

### Timeout/budget behavior

`DisplayTessellationExecutionBudget` provides an elapsed/remaining budget and throws `DisplayTessellationTimeoutException`. `BrepDisplayTessellator` catches that exception and returns a `Viewer.Tessellation.Timeout` diagnostic. The current behavior prevents an indefinite hang but still fails the entire tessellation result and therefore the entire fallback display preparation request.

### Current tests

Relevant tests include:

- FTC-07 view materialization regression tests in `Step242Ftc07ViewMaterializationRegressionTests.cs`.
- Server display/prepare integration tests in `KernelApiIntegrationTests.cs` for analytic-only, mixed fallback, fallback-only, and FTC-07 bounded failure behavior.
- Client `App.test.tsx` includes behavior that reports view materialization failure without presenting it as import failure.
- Additional tessellation and display-related tests are discoverable through filters containing `Tessellation`, `Display`, `ViewMaterialization`, `FTC07`, or `Ftc07`.

### Hot path or fallback?

It is both:

- direct hot path for `/tessellate`; and
- implicit fallback hot path for `/display/prepare` whenever analytic coverage is incomplete.

That second role is the architecture smell.

## 5. FTC-07 specific finding

Existing X2 evidence says:

- `analyze` succeeds for `testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp`.
- `canon` succeeds and writes a canonical STEP file.
- server import returns successfully before display materialization.
- `display/prepare` returns a bounded 422 instead of hanging.
- the diagnostic source is `Viewer.Tessellation.Timeout`.
- the failing face is face 9.
- the surface is `Plane`.
- the phase is `PlanarTriangulationWithHoles`.

Therefore FTC-07 should be treated as a view/materialization problem, not as evidence that AP242 import/export semantics are broken.

## 6. CIR as display source

### Does CIR currently exist in a form suitable for display sampling?

CIR exists as a constructive/evaluable representation with nodes, tapes, mirrors, analyzers, and differential tests. It is not currently wired into `display/prepare`, the display DTOs, or the Three.js viewer as a display source. Existing docs describe CIR/FRep as an analysis mirror/evaluation side-channel, not the default topology construction or display authority.

### What would CIR display mean?

A CIR-driven display would need one of the following:

- CPU field sampling plus contouring/mesh extraction;
- GPU shader evaluation/raymarching;
- analytic recognition from CIR nodes into display primitives;
- hybrid preview proxies for known primitives and field sampling for unsupported composition.

None of these is currently present as a production viewer path.

### Would CIR preserve exactness?

Not automatically. Field sampling, contouring, and raymarching are approximate unless paired with exact implicit intersection and exact trim/topology handling. CIR could preserve semantic intent for supported primitives, but the moment it is sampled into voxels, marching cubes, signed-distance grids, or fixed shader steps, it becomes approximate display.

### Would CIR fit Three.js?

Three.js can display CIR-derived meshes or run custom shaders, but the current viewer is not structured that way. A CIR mesh extraction path would still produce triangles. A shader path would require a different material/rendering architecture, picking model, depth behavior, clipping/section strategy, and diagnostics.

### Performance and risk

CIR display might be useful for previews and unsupported imported topology diagnostics, but it is risky as the main exact CAD display path:

- imported STEP BReps do not necessarily have CIR provenance;
- CIR mirrors intentionally have known losses such as face identity/topology parity in some lanes;
- field sampling resolution creates accuracy/performance tradeoffs similar to tessellation;
- GPU raymarching creates a second rendering truth distinct from backend BRep.

Conclusion: CIR display should remain future/experimental. It should not replace BRep-derived display authority for imported CAD in this milestone.

## 7. Analytic display methodology

### What can be displayed directly today?

The server can emit analytic descriptions for:

- planes;
- cylinders;
- cones;
- spheres;
- tori.

However, support is constrained by trim policy. Analytic support is not equivalent to “all trimmed analytic BRep faces can be displayed exactly.” The packet records fallback faces when surface kind or trim structure is unsupported.

### What does the frontend do with analytic data?

The frontend converts analytic surfaces into triangles:

- planes: simple polygon triangulation of `outerBoundary` only;
- cylinders/cones: fixed angular segment strips between V bounds;
- spheres: fixed longitude/latitude grid;
- tori: fixed major/minor segment grid.

Edges/wires are only rendered from tessellation fallback edge polylines; `mapAnalyticPacketToRenderData` returns no analytic edges.

### Is analytic display currently exact?

No. It is better described as **analytic source DTOs with frontend mesh materialization**. This avoids the legacy backend tessellator for analytic-only bodies, which is valuable, but Three.js still renders triangles and the frontend owns sampling density for those faces.

### What would a real analytic viewer require?

A true analytic viewer would require at least one of:

- shader-based analytic surface intersection/evaluation with depth integration;
- screen-space analytic curves/edges and exact clipping/trim masks;
- GPU or CPU ray/surface rendering with robust picking parity;
- explicit wire/loop/trim DisplayIR so boundaries, holes, silhouettes, and section curves are first-class;
- partial display/proxy semantics for unsupported faces.

That is larger than a cleanup of the existing mapper.

## 8. Tessellation as explicit lowering target

Tessellation should remain, but only as an explicit bounded lowering target. It is still useful for:

- export/debug previews;
- unsupported viewer environments;
- cached low/medium/high preview meshes;
- localizing problematic faces;
- mesh-only downstream integrations.

It should not be the implicit fallback authority for all display completeness.

### What should it lower from?

Recommended source hierarchy:

1. **DisplayIR analytic/wire primitives** for simple viewport meshes and debug proxies.
2. **BRep exact faces** for face-specific bounded mesh lowering where topology identity matters.
3. **CIR** only for explicitly marked preview experiments, not imported STEP display truth.

Avoid lowering from AIR/BRepPlan for imported STEP unless a recognizer has explicitly recovered that provenance. Avoid making CIR the default source for imported BRep display.

### Operating model

- Tessellation should be opt-in or quality-level controlled.
- It should be cached per face/body/options where practical.
- It should be bounded per face, not just per body.
- Failure should produce a partial DisplayIR patch: wireframe, bounding proxy, diagnostic patch, or omitted face with explicit reason.
- The display endpoint should be able to succeed with partial materialization.

## 9. Display IR proposal

A `DisplayIR`/`ViewIR` is a good abstraction because the current DTO already wants to be one but lacks explicit partial/proxy/failure semantics.

Suggested shape:

```text
DisplayBody
  body id / occurrence id / transform / bounds / display mode summary
DisplayFace
  face id / shell id / role / status / source authority / diagnostics
AnalyticPatch
  plane/cylinder/cone/sphere/torus + bounded trim metadata
DisplayWire / DisplayLoop / DisplayCurve
  exact or sampled boundary/edge representation with role and quality metadata
MeshPatch
  positions/normals/indices/source/quality/options/cache key
DiagnosticPatch
  face id/reason/source/phase/elapsed/next action
BoundingBox / PreviewProxy
  fallback visual envelope
```

Modes:

```text
ExactAnalytic
BoundedMesh
WireframeOnly
BoundingProxy
DiagnosticOnly
```

### Server emission

`display/prepare` should emit a DisplayBody with per-face records. For each face, the server can select the best available lane:

1. analytic patch plus wires if admitted;
2. bounded mesh patch if requested/admitted and budgeted;
3. wireframe-only from BRep edges;
4. bounding/diagnostic proxy;
5. explicit failed/omitted face record.

This turns failure from a response-level 422 into face-level display degradation for view-only materialization.

### Three.js consumption

Three.js should render DisplayIR records by type:

- analytic patch: currently sampled client-side mesh, later shader/analytic renderer;
- mesh patch: direct `BufferGeometry`;
- wire/loop/curve: line renderer;
- diagnostic patch: highlighted proxy or annotation;
- bounding proxy: transparent box/face marker.

The frontend should not silently invent fallback geometry without the server labeling the approximation and source.

## 10. Recommended architecture

```text
Recommended authority:
  BRep remains geometry/topology authority.
  DisplayIR becomes view authority.
  Tessellation becomes one bounded DisplayIR lowering lane, not fallback authority.
  CIR display remains future/experimental.
  Three.js renders DisplayIR primitives/proxies/meshes, but does not own geometric truth.
```

### Short-term path

1. Introduce DisplayIR semantics in the existing `DisplayPreparationResponseDto` shape or a v2 DTO.
2. Make display preparation partial-success by default for view materialization.
3. Emit wireframe/diagnostic records for faces whose mesh lowering times out.
4. Keep current analytic DTOs but label frontend sampling as an approximate render lane.
5. Keep FTC-07 import/export untouched.

### Medium-term path

1. Quarantine `BrepDisplayTessellator` behind an explicit `BoundedMesh` lowering lane.
2. Rewrite planar-with-holes as a small, tested lane with clear loop classification, no unbounded retries, and face-level failure.
3. Add DisplayIR tests that assert unsupported/timed-out faces do not fail the whole display response.
4. Move frontend rendering from “mesh scene data” to typed DisplayIR rendering.
5. Evaluate analytic shader/wire improvements after DisplayIR makes fallback semantics explicit.

### Delete/quarantine/keep/rebuild

- **Keep** BRep as authority and current analytic packet work as a useful seed.
- **Keep but quarantine** the legacy tessellator for explicit mesh lowering, tests, and diagnostics.
- **Rebuild** planar-with-holes lowering as a bounded face-local component.
- **Rebuild** frontend display around typed DisplayIR rather than assuming every visual face is a triangle patch.
- **Do not use** CIR as default display authority for imported STEP.

## 11. Risk matrix

| Option | Benefit | Main risk | Recommendation |
|---|---|---|---|
| Keep legacy tessellator as implicit fallback | Fastest path to broad visible coverage | Reintroduces FTC-07 hangs/timeouts and hides display authority | Do not keep as implicit fallback |
| Rewrite tessellator | Can improve mesh quality and reliability | Large scope; may become another universal fallback trap | Rewrite only bounded lanes, starting planar holes |
| Analytic-only display | Avoids legacy backend tessellator for admitted cases | Current Three.js path still meshes; unsupported trims vanish/fail | Good for supported subset, not complete strategy |
| CIR-driven display | Potential preview for constructive fields | Approximate sampling, provenance gaps for STEP, new renderer complexity | Future experiment only |
| DisplayIR hybrid | Explicit authority, partial display, diagnostics | Requires DTO/frontend refactor | Recommended |
| Frontend tessellation | Low server cost; responsive for simple primitives | Browser owns approximation and can duplicate kernel bugs | Allow only labeled view lowering, not truth |
| Backend tessellation | Centralized, testable, can use BRep topology | Can block display and fail whole response if universal | Use bounded per-face, cached, partial |

## 12. Proposed milestones

### DISPLAY-ARCH-X1 — DisplayIR contract and bounded partial display

- **Goal:** define and emit per-face DisplayIR/ViewIR records with analytic, mesh, wireframe, proxy, and diagnostic states.
- **Scope:** server DTOs, mapping from current analytic packet/fallback result, client renderer adapter, tests for partial success.
- **Non-goals:** new tessellator algorithms, CIR renderer, STEP changes.
- **Tests:** server display/prepare returns success with diagnostic face records when a bounded mesh lane fails; client renders available faces/wires and reports missing faces.

### DISPLAY-ARCH-X2 — Quarantine legacy tessellator behind explicit lowering lane

- **Goal:** make `BrepDisplayTessellator` an explicit `BoundedMesh` lane with quality/budget/options metadata.
- **Scope:** API labels, per-face budget reporting, no implicit fallback authority.
- **Non-goals:** broad geometry rewrite.
- **Tests:** analytic-only bodies do not invoke backend tessellation; fallback bodies report mesh-lane source and per-face diagnostics.

### DISPLAY-ARCH-X3 — Planar face with holes display lane rewrite

- **Goal:** replace or isolate `PlanarTriangulationWithHoles` with a bounded, deterministic, face-local implementation.
- **Scope:** loop classification, nested holes/islands policy, triangulation failure diagnostics, FTC-07 face 9 fixture.
- **Non-goals:** NURBS, Boolean, importer changes.
- **Tests:** FTC-07 face 9 returns either valid mesh or bounded diagnostic/proxy under per-face budget; no full display failure.

### DISPLAY-ARCH-X4 — Wireframe/edge-first fallback for unsupported faces

- **Goal:** ensure every imported BRep can produce at least visible edges/bounds when face materialization is unsupported.
- **Scope:** DisplayWire/DisplayLoop DTOs, edge polyline generation, frontend line rendering.
- **Non-goals:** surface fill for every face.
- **Tests:** unsupported surface kind still displays edges and diagnostic face entries.

### DISPLAY-ARCH-X5 — Three.js DisplayIR renderer cleanup

- **Goal:** replace `RenderSceneData` as the top-level viewer contract with typed DisplayIR rendering.
- **Scope:** analytic/mesh/wire/proxy components, warnings for approximate lanes, client tests.
- **Non-goals:** custom GPU analytic renderer.
- **Tests:** analytic-only, mixed, fallback-only, and diagnostic-only fixtures render expected component types.

### DISPLAY-ARCH-X6 — Optional CIR preview experiment

- **Goal:** prototype CIR-derived preview for explicitly constructed CIR fixtures only.
- **Scope:** one or two admitted primitive/Boolean examples, clearly labeled approximate preview.
- **Non-goals:** imported STEP display, topology authority, exact CAD display.
- **Tests:** preview is opt-in and never used for imported BRep display without explicit provenance.

## 13. Immediate recommendation for FTC-07

The next FTC-07 action should be **implement bounded partial display fallback before fixing planar triangulation with holes**.

Reason: FTC-07 already proves import/export succeeds and display materialization can fail on one face. Fixing face 9 triangulation may solve this fixture but leaves the architecture vulnerable to the next pathological face. A partial DisplayIR/wireframe/diagnostic path converts display materialization from all-or-nothing to robust view degradation. After that, rewrite the planar-with-holes lane as DISPLAY-ARCH-X3 with FTC-07 face 9 as a regression fixture.

Concrete next action:

```text
DISPLAY-ARCH-X1 first:
  display/prepare returns success with DisplayFace face 9 marked DiagnosticOnly or WireframeOnly when PlanarTriangulationWithHoles times out.
  The UI shows the rest of the body plus a diagnostic/wireframe marker.
  The import status remains success and the view status reports partial materialization.
```

## 14. Non-goals

This audit does not and should not:

- implement general NURBS;
- implement general Boolean;
- rewrite the kernel;
- change STEP import/export;
- change Firmament V2 side-hole semantics;
- change AIR Region route policy;
- change CIR authority;
- change Firmasm;
- change CAD feature behavior;
- claim FTC-07 kernel import is broken when evidence points to display materialization.

## Validation notes

This milestone is documentation-only. No production CAD/kernel behavior was changed. Playwright was not used; code inspection, existing tests, and CLI/server validation were sufficient to map the current display path.

## X1 follow-up

DISPLAY-ARCH-X1 adds the first DisplayIR partial display contract. `display/prepare` now carries source/display authority metadata and per-face display records so a bounded face materialization timeout can become a diagnostic face rather than a whole-body display preparation failure. See `docs/development/milestones/general/display-architecture-x1-displayir-partial-display.md`.

## X2 implementation note

DISPLAY-ARCH-X2 landed the explicit `BoundedMesh` DisplayIR lane. Legacy BRep tessellation remains available for display materialization, but new `display/prepare` metadata reports it as a bounded lowering lane with `source = BRep` and `displayAuthority = DisplayIR`, preserving compatibility fields while removing generic fallback-authority semantics from new DisplayIR lane data.

### DISPLAY-ARCH-X5 frontend update

DISPLAY-ARCH-X5 landed the frontend renderer cleanup recommended by A0: typed DisplayIR records are mapped into `DisplayScene` / `DisplayRenderable` records and rendered by `AetherisViewport`, with Three.js kept as backend plumbing. Legacy `RenderSceneData` compatibility remains only when typed DisplayIR face records are unavailable.
