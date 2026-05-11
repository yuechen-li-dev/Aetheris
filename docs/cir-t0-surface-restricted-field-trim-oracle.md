# CIR-T0: surface-restricted field / trim oracle design

## 1) Problem statement

Current CIR/F8 readiness and candidate generation treat trim support primarily as a **surface-family pair classification** (`TrimCapabilityMatrix`) and do not yet compute retained trim loops from the opposite operand field. This becomes the bottleneck for high-value deferred families (notably planar×toroidal and several cylindrical/spherical combinations) even when both operands are evaluable in CIR.  

CIR-T0 defines the architecture pivot:

- from: `source family × opposite family -> static capability`
- to: `source surface S(u,v) + opposite CIR field F(x,y,z) -> restricted field g(u,v)=F(S(u,v)) -> contours + retained region sign`

Scope guardrails for T0:

- design/probe only (no production contour extraction yet),
- no BRep topology behavior changes,
- no STEP behavior changes,
- no CIR materialization changes,
- no surface-feature behavior changes.

## 2) Definition of a surface-restricted field

### Proposed descriptor: `SurfaceRestrictedField`

```text
SurfaceRestrictedField
  SourceSurfaceId / Provenance
  SourceDescriptor (SourceSurfaceDescriptor)
  ParameterizationContract (IRestrictedSurfaceParameterization)
  Domain2D (uMin,uMax,vMin,vMax + seam flags)
  OppositeSelection (operand role + source/opposite subtree ids)
  OppositeField (CirTape preferred; CirNode fallback for diagnostics)
  ToleranceContext
  OrientationPolicy
  Evaluate(u,v) -> RestrictedFieldSample
  EvaluateInterval(domainCell2D) -> FieldInterval (future)
  Diagnostics (construction + sampling provenance)
```

### Why `CirTape` first, `CirNode` second

- `CirTape` is already the runtime execution form with primitive payload transforms and point evaluation, and has interval classification infrastructure that can be adapted conceptually to 2D cells after mapping into world-space bounds.
- `CirNode` should remain available as semantic oracle and for debugging parity checks, but the restricted-field runtime should execute tape-first to stay aligned with CIR-E architecture.

### How “opposite” is encoded

In subtract roots, opposite is role-dependent:

- source from left/base subtree => opposite is right/tool subtree,
- source from right/tool subtree => opposite is left/base subtree.

`SurfaceRestrictedField` should explicitly carry:

- `SourceOperandRole` (`Base`/`Tool`),
- `OppositeOperandRole`,
- subtree identity (at least replay op index + operand side; eventually stable subtree key).

### Relationship to source descriptors

`SourceSurfaceDescriptor` remains the geometry/provenance anchor. `SurfaceRestrictedField` is derived from one descriptor plus one opposite field tape and an explicit parameterization contract. It is not a replacement for descriptor extraction; it is the execution layer above it.

## 3) Current infrastructure audit (what exists now)

### CIR evaluation runtime

- `CirNode` and concrete nodes (box/cylinder/sphere/torus/transform/union/subtract/intersect) provide semantic SDF evaluation and bounds.
- `CirTapeLowerer` composes transforms into inverse payload transforms and lowers booleans to `Min/Max/Neg` instruction sequences.
- `CirTape.Evaluate` provides pointwise field values; `EvaluateInterval` provides conservative 3D AABB intervals.
- `CirRegionPlanner` already uses `JudgmentEngine` for admissibility/utility-based decision among classify/subdivide/sample options.
- `CirAdaptiveVolumeEstimator` already operationalizes planner decisions, adaptive splitting, direct sampling, and trace diagnostics.

### Surface descriptor/readiness infrastructure

- `SourceSurfaceExtractor` inventories primitive-origin surfaces for box/cylinder/sphere/torus and composes transforms.
- `BoundedPlanarPatchGeometry` exists for rectangle and circle; cylindrical caps can emit circle geometry when transform preserves circularity.
- `CylindricalSurfaceGeometryEvidence` carries axis/radius/height plus bottom/top centers.
- `FacePatchCandidateGenerator` currently uses `TrimCapabilityMatrix` pair lookups plus subtract-role heuristics; loop descriptors are scaffolding, not contour-derived.
- `MaterializationReadinessAnalyzer` aggregates evidence layers; explicitly dry-run and conservative.

### Existing docs that frame this pivot

- CURVE-A0 recommends tiered trim representation with surface-intersection provenance first, analytic realization second, numerical cache third.
- CIR-F8/F8.2/F8.10 establish descriptor-first and readiness-gated architecture with deferred torus/general algebraic cases.
- CIR-F15/F16 establish internal identity/token diagnostics for future stitching.
- SURFACE-FEATURE A0-A4 establish descriptor/planning/evidence patterns and explicit exactness honesty.

## 4) Parameterization readiness by surface family

### A) Planar bounded rectangle (box faces): **ready first**

Available now:

- 4 corners (`Corner00/10/11/01`) in world coordinates,
- plane normal,
- finite rectangular domain.

T0 contract:

- `u` axis: `Corner00 -> Corner10`, `v` axis: `Corner00 -> Corner01`,
- domain `[0,1] × [0,1]` (or metric extents, but normalized recommended first),
- mapping `S(u,v)=Corner00 + u*(Corner10-Corner00) + v*(Corner01-Corner00)`.

### B) Planar circular cap (cylinder caps): **near-ready second**

Available now:

- `BoundedPlanarPatchGeometry.Kind=Circle` with center/normal/radius (when transform admissible).

Missing/choice:

- canonical in-plane axis frame for stable `(u,v)`/polar mapping.

T0 recommendation:

- choose disk-local rectangular parameterization over polar for T2/T3 simplicity,
- domain `[-R,R]×[-R,R]` with in-disk predicate for valid samples,
- defer exact seam/orientation intricacies until post-T3.

### C) Cylindrical side: **ready with seam policy definition**

Available now:

- `CylindricalSurfaceGeometryEvidence` axis origin/direction, radius, height, end centers.

Needed contract:

- parameters `(theta,z)` with `theta` in `[0,2π)` and `z` in `[0,height]` (or centered range),
- explicit seam duplication policy for contour extraction at theta wrap,
- world mapping `S(theta,z)=axisPoint(z)+radius*(xFrame*cos(theta)+yFrame*sin(theta))`.

### D) Spherical: **descriptor thin, parameterization not yet contracted**

Current `SourceSurfaceDescriptor` for sphere has family/provenance but no typed sphere geometry evidence (center/radius/frame). This is insufficient for robust restricted parameter mapping.

### E) Toroidal: **later; geometry evidence missing for parameterization contract**

Current torus descriptors are family/provenance only plus deferred diagnostics; no typed major/minor/frame evidence contract.

### First target recommendation for CIR-T1

**Planar bounded rectangle** should be first target due to strongest current evidence shape, no seam singularity, and immediate coverage of box-face subtract scenarios.

## 5) Opposite operand selection plan

### Subtract semantics (first contract)

For `CirSubtractNode(A,B)`:

- for descriptors originating from `A`: opposite field is `Lower(B)`;
- for descriptors originating from `B`: opposite field is `Lower(A)`.

### Can current code identify source/opposite reliably?

Partially:

- current code uses `IsFromNode` by comparing `SourceSurfaceDescriptor.OwningCirNodeKind` to `node.GetType().Name`; this is not robust when both sides share primitive type (e.g., cylinder minus cylinder).

Missing for robust trim-oracle routing:

- stable per-descriptor subtree identity (operand path or unique node id),
- explicit operand-side provenance (`Left`/`Right`) at extraction time.

### Contract addition recommended

Extend descriptor provenance metadata (internal only) with:

- `OwningOperandPath` (e.g., `L`, `R`, `LLR`),
- `OwningBooleanRootOpIndex` where available,
- deterministic source/opposite resolution helper for subtract roots.

Union/intersect semantics can be deferred but conceptually map to sign rules over each side’s field; no need to block subtract-first milestone.

## 6) Relationship to `TrimCapabilityMatrix`

`TrimCapabilityMatrix` should evolve from hard gate to layered helper:

1. **Coarse prior / planning hint**: expected curve families, risk class, and likely snap candidates.
2. **Post-contour classifier**: label extracted contours as line/circle/ellipse/implicit candidate.
3. **Snap registry input**: choose analytic snap strategies allowed for a pair.

It should no longer be the sole determinant of “can we trim at all.”

Impact on torus cases:

- planar×toroidal can move from blanket deferred to “restricted-field contour available; exact snap/export deferred,” enabling meaningful progression without false exactness.

## 7) Sampling/contouring plan (design only)

### Baseline

1. Build restricted field on parameter domain.
2. Sample regular grid of scalar values/signs.
3. Classify 2D cells (inside/outside/mixed).
4. Extract zero contours via marching squares.
5. Lift polyline samples to 3D via `S(u,v)`.

### Reuse from CIR runtime

Reusable immediately:

- opposite field evaluation via `CirTape.Evaluate`,
- tolerance semantics (`ToleranceContext`) and sign classification idioms,
- planner pattern from `CirRegionPlanner`/`CirAdaptiveVolumeEstimator`.

Needs 2D equivalents/new components:

- 2D domain/cell structures,
- 2D interval heuristic (optional early; can start sample-first),
- marching squares implementation and contour stitching,
- seam-aware contour assembly for periodic surfaces.

### JudgmentEngine placement

Use `JudgmentEngine` once multiple bounded strategies compete, e.g.:

- subdivide-vs-direct-sample decisions per 2D cell,
- snap candidate choice (circle/line/implicit polyline),
- exact-snap vs numerical-only vs deferred-export route.

## 8) Output model proposal

Proposed internal records for T2+:

```text
SurfaceFieldSampleGrid
  SurfaceRestrictedFieldId
  Domain2D
  ResolutionU/ResolutionV
  Samples[][] (value, sign, worldPoint optional)
  Diagnostics

SurfaceTrimContour2D
  LoopId
  Closed
  PolylineUV
  Orientation
  ExtractionMethod (MarchingSquares/Other)
  Confidence

SurfaceTrimContour3D
  LoopId
  PolylineXYZ
  SourceContour2DId
  LiftDiagnostics

SurfaceRegionClassification2D
  RegionId
  RepresentativePointUV
  SignClass (Inside/Outside/Mixed/Unknown)
  SupportingSamples

SurfaceRestrictedFieldDiagnostic
  Code
  Message
  Severity
  SourceProvenance
```

Alignment with CURVE-A0 tiering:

- Tier 1: analytic snap result (when available),
- Tier 2: surface-intersection provenance bundle (source surface + opposite field + extraction metadata),
- Tier 3: numerical contour cache/polylines with explicit non-exact flags.

## 9) Materialization pipeline integration plan

### Near-term integration points

- `SourceSurfaceExtractor`: add typed parameterization payloads (first planar rectangle) and operand-path provenance.
- `FacePatchCandidateGenerator`: for subtract candidates, request restricted-field evidence instead of only pair-table best score; retained loops become evidence-backed.
- `TrimCapabilityMatrix`: demote to hint/classifier/snap policy layer.
- `MaterializationReadinessAnalyzer`: add layer signals such as
  - `restricted-field-evidence-ready`,
  - `contour-extracted-numerical-only`,
  - `analytic-snap-ready` / `export-deferred`.

### Connection to existing loop descriptors

`RetainedRegionLoopDescriptor` should gain provenance mode fields (future milestone):

- `LoopProvenanceKind` (`MatrixOnly`, `RestrictedFieldNumeric`, `RestrictedFieldSnappedAnalytic`),
- contour ids/snap diagnostics,
- export capability classification.

This lets `PlanarSurfaceMaterializer` remain unchanged initially while readiness/diagnostics become more truthful for deferred pairs.

### Surface-feature future

The same restricted-field oracle supports groove-like host-surface bounded features by evaluating feature-local tool fields on host parameter domains, consistent with SURFACE-FEATURE descriptor/planning/evidence architecture.

## 10) Export/exactness policy

Hard policy:

- Numerical contours are **not** exact AP242 trims by themselves.
- Exact export requires analytic snap or exact realizable trim curve representation supported by current exporter/importer lanes.
- CIR preview/readiness can consume numerical contours with explicit non-exact/deferred export diagnostics.
- No path may silently promote numerical polylines to exact trim claims.

## 11) Risks and guardrails

Risks:

1. parameterization frame mistakes (uv->xyz misalignment),
2. seam discontinuity errors (cylinder periodic wrap),
3. sampling aliasing/missed small components,
4. contour branching/self-intersection ambiguities,
5. tangent/grazing zero-set instability,
6. singularities (sphere poles, torus inner radius regimes),
7. tolerance sign flips around zero,
8. performance blow-up from brute grids,
9. false exactness in downstream readiness/export messaging.

Guardrails:

- mandatory diagnostics for frame/domain construction,
- deterministic tolerance band policy near zero,
- minimum and adaptive maximum sampling controls,
- contour validity checks (closedness, degeneracy, area thresholds),
- explicit `ExportCapability` field on contour/snap outputs,
- conservative readiness downgrade on ambiguous topology,
- regression fixtures for known tangent and seam-crossing cases.

## 12) Implementation ladder

### CIR-T1

Planar rectangular restricted-field evaluator only:

- construct `SurfaceRestrictedField` from planar rectangle descriptor,
- choose opposite tape for subtract,
- evaluate sample probes `(u,v)->F(S(u,v))`,
- no contour extraction yet.

### CIR-T2

- regular sample grid + sign classification on planar rectangles,
- diagnostics and retention-side sign summaries.

### CIR-T3

- marching squares contour extraction on planar rectangles,
- closed/open loop diagnostics.

### CIR-T4

- analytic snap for planar line/circle first,
- snap confidence and rejection diagnostics.

### CIR-T5

- feed contours/snap into `RetainedRegionLoopDescriptor` provenance,
- readiness analyzer integration for numerical-vs-exact policy.

### CIR-T6

- cylindrical side restricted field (`theta,z`) with seam policy.

### CIR-T7

- surface-feature groove prototype using restricted-field evidence path.

## 13) Recommended CIR-T1 next step

Proceed with **CIR-T1 = planar rectangular restricted-field evaluator** under subtract roots only, with the following acceptance checks:

1. build restricted fields for box planar faces with explicit uv frame/domain diagnostics,
2. resolve opposite operand tape by subtract side,
3. evaluate deterministic probe set and verify sign behavior for:
   - box face vs cylinder field,
   - box face vs sphere field,
   - box face vs torus field,
4. emit readiness diagnostics explicitly stating: evaluation/oracle available, contour extraction not yet implemented, export unchanged.

This gives a low-risk, high-evidence bridge into T2/T3 while respecting all no-behavior-change constraints.
