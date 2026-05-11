# CURVE-A0: Trim-Curve Representation Audit (Exact Quartic / Surface-Intersection Feasibility)

## Outcome

**Outcome A — clear recommendation.**

Aetheris should **not** start with a standalone `QuarticCurve3D` primitive. The best next representation direction is a **tiered trim representation centered on exact surface-intersection provenance**, with optional analytic curve realization (line/circle/ellipse/BSpline) and numerical contour cache.

## Scope and constraints

This audit is design-only. No BRep topology behavior, CIR materialization behavior, or STEP behavior changes are proposed in this milestone.

---

## 1) Current curve support table

| Curve type | Internal representation | Parametric eval? | Trim interval support? | BRep edge binding? | STEP export? | STEP import/recovery? | Test coverage (examples) |
|---|---|---|---|---|---|---|---|
| Line | `Line3Curve` | Yes (`Evaluate`, `Tangent`) | Yes (`EdgeGeometryBinding.TrimInterval`) | Yes (`CurveGeometryKind.Line3`) | Yes (`LINE`) | Yes (`DecodeCurveGeometry` line path) | `CurvePrimitivesTests`, many `Step242*` |
| Circle | `Circle3Curve` | Yes (`Evaluate`, `Tangent`) | Yes (angle interval via `ParameterInterval`) | Yes (`CurveGeometryKind.Circle3`) | Yes (`CIRCLE`) | Yes (`DecodeCurveGeometry` circle + trim compute) | `CurvePrimitivesTests`, circle STEP regressions |
| Ellipse | `Ellipse3Curve` | Yes (`Evaluate`) | Yes (angle interval via `ComputeEllipseTrim`) | Yes (`CurveGeometryKind.Ellipse3`) | Yes (`ELLIPSE`) | Yes (`DecodeCurveGeometry` ellipse path) | `Step242EllipseEdgeRegressionTests`, planar ellipse diagnostics |
| BSpline | `BSpline3Curve` | Yes (`Evaluate`, knot-domain aware) | Yes (uses spline `DomainStart/DomainEnd`) | Yes (`CurveGeometryKind.BSpline3`) | Yes (`B_SPLINE_CURVE_WITH_KNOTS`) | Yes (`ReadBSplineCurveWithKnots` lane in importer) | `Step242BSplineEdgeTests`, BSpline geometry tests |
| Polyline / multi-line | No first-class curve primitive; represented as multiple line edges | Per edge only | Per edge only | Indirect (many `Line3Curve` edge bindings) | Indirect (`LINE` per segment) | Indirect (imports as separate line edges) | topology/export tests using segmented edges |
| Unsupported placeholder | `CurveGeometryKind.Unsupported` + name | No | Synthetic `[0,1]` used in importer fallback | Bindable but non-exportable | No (export rejects unsupported kind) | Limited importer placeholder for named unsupported planar curve | unsupported STEP diagnostics tests |

Observations:
- The curve model is a discriminated union over a **small, explicit, evaluable** set.
- Edge geometry is always carried as `EdgeGeometryBinding(EdgeId, CurveGeometryId, TrimInterval?)`, so interval semantics are first-class even for analytic primitives.

---

## 2) Ellipse implementation audit

Ellipse is implemented as a **narrow, explicit parametric primitive** (`Ellipse3Curve`) with:
- center, normal, major/minor radii, reference axis,
- validated orthonormal frame (`XAxis`, `YAxis`),
- parametric `Evaluate(t)`.

### Export behavior
Exporter emits STEP `ELLIPSE` via axis placement + radii, then references it from `EDGE_CURVE`.

### Import/recovery behavior
Importer detects `ELLIPSE`, decodes to `Ellipse3Curve`, computes trim interval from edge endpoints (`ComputeEllipseTrim`), and binds the interval to edge geometry.

### Closed loops and trims
- Closed/full ellipse can be represented by interval coverage policy (periodic parameter semantics).
- Partial ellipse trims are represented by `ParameterInterval` on edge binding.

### Why “quartic like ellipse” is a weak analogy
Ellipse works because it has:
1. native evaluable parameterization,
2. unambiguous local frame,
3. stable endpoint-to-parameter inversion path in importer,
4. direct STEP primitive mapping (`ELLIPSE`).

A generic quartic implicit curve lacks these out of the box and would need substantial new kernel infrastructure.

---

## 3) Current STEP entity support (as implemented)

### Exporter (`Step242Exporter`)
Supported curve emission:
- `LINE`
- `CIRCLE`
- `ELLIPSE`
- `B_SPLINE_CURVE_WITH_KNOTS`

Not emitted in current code:
- `SURFACE_CURVE`
- `INTERSECTION_CURVE`
- `PCURVE`
- `DEFINITIONAL_REPRESENTATION`
- `CURVE_REPLICA`
- `TRIMMED_CURVE`
- generic algebraic implicit curve entities

Edges are exported as `EDGE_CURVE(..., geometryCurve, .T.)` bound to one 3D curve.

### Importer (`Step242Importer`)
Recovered curves in `DecodeCurveGeometry`:
- `LINE`
- `CIRCLE`
- `ELLIPSE`
- `B_SPLINE_CURVE_WITH_KNOTS` (including normalized split constructor form)

No surface-curve/pcurve lane is present in importer curve binding path; no intersection-curve decode path is used for BRep edge construction here.

### Net audit conclusion
Current STEP lane is **3D edge-curve primitive based**, not curve-on-surface based.

---

## 4) `QuarticCurve3D` feasibility

A usable generic quartic edge type would require, at minimum:
1. Representation:
   - implicit polynomial in 3D (typically as intersection of surfaces) or explicit parametric branch data,
   - robust branch identity (multiple connected components possible).
2. Traversal/parameterization:
   - arc/continuation parameter, not just algebraic membership,
   - stable monotone interval per edge segment.
3. Evaluation:
   - `Evaluate(t)` and tangent/normal-like differential data for tessellation and orientation.
4. Domain and trim handling:
   - bounded interval support, periodic branch handling, singularity guards.
5. Topology compatibility:
   - endpoint solving for vertices,
   - consistent orientation, seam behavior, and loop closure tests.
6. Distance/projection utilities:
   - needed by sampling/import diagnostics and likely snapping/validation.
7. Visualization/tessellation:
   - adaptive sampling with error controls.
8. STEP I/O strategy:
   - current exporter/importer has no direct algebraic quartic lane; would need substantial extension.
9. Face-domain coupling:
   - if used as trim on face, likely needs pcurve or on-surface evaluator anyway.

**Size judgment: giant.** This is not an “add one record type” change; it is a curve-kernel subsystem.

---

## 5) `SurfaceIntersectionTrimCurve` feasibility

Candidate concept:
- `SurfaceIntersectionTrimCurve(surfaceA, surfaceB, optional domainA/domainB, numerical cache, optional analytic snap)`.

### Fit vs BRep needs
- Better fit than raw quartic because trims originate from **face-surface intersections**.
- Preserves exact provenance (which two surfaces generated the trim).
- Allows per-face mapping (future pcurve/domain) without committing immediately to global quartic algebra.

### STEP implications
- To export exact intersection semantics in STEP, eventually needs support for entities such as `SURFACE_CURVE` / `INTERSECTION_CURVE` and/or defensible conversion to existing supported primitives when snap succeeds.
- With current exporter, direct exact export is blocked unless snapped to line/circle/ellipse/BSpline.

### Plane × torus applicability
- Yes: can represent provenance immediately and defer export classification until analytic snap or approximation policy decision.

### Limitations
- Still needs numerical contouring/branch isolation machinery.
- Without STEP extension, remains internal-only for exactness provenance.

---

## 6) Tiered trim representation feasibility

Recommended abstraction:
- `analyticCurve?` (line/circle/ellipse/BSpline)
- `surfaceIntersection?` (exact source provenance)
- `numericalContour?` (sampled/refinable)
- `exportCapability`
- `refinementSource`

### Consumer mapping
- CIR analysis/dry-run: use provenance + capability classification.
- Preview/rendering: use numerical contour.
- Picking: use numerical contour + optional local refinement.
- BRep topology assembly: use oriented segment + endpoint identity built from chosen tier.
- STEP export: prefer analytic exact export; otherwise classify as deferred/unsupported until new STEP lane exists.

### Philosophy alignment
This keeps analytic-first behavior where available while enabling progress for hard intersections without pretending current STEP exactness exists.

---

## 7) Plane × torus recommendation

- Direct generic quartic: **not recommended as first move**.
- Surface-intersection representation: **recommended**.
- Numerical contour + analytic snap: **recommended as companion**.

Current exporter cannot represent exact plane×torus intersection unless reduced to supported primitives; general quartic branch is not directly exportable in current lanes.

### Practical guidance for `box - torus` today
Materialization should continue to report deferred/unsupported for exact analytic trim emission, with explicit diagnostics that intersection trim representation/export lane is missing. This is consistent with existing torus-deferred diagnostics and `subtract(box,torus)` materializer status.

---

## 8) `TrimCapabilityMatrix` evolution

Current matrix is static by surface-family pair and already marks several pairs as `SpecialCaseOnly` or `Deferred`.

Recommended evolution:
1. Keep family-pair matrix as coarse prior.
2. Add **per-instance trim classifier** (orientation/offset/coaxial/skew/separation, etc.).
3. Add special-case detector registry (e.g., planar+toroidal slice classes).
4. Add routing using `JudgmentEngine` where multiple bounded strategies compete:
   - exact analytic snap,
   - surface-intersection retained,
   - numerical contour fallback,
   - defer/reject with reason.

This addresses the core truth: trim feasibility is not only family-pair dependent; it is configuration dependent.

---

## 9) Implementation ladder

1. CURVE-A1: introduce tiered trim representation types (internal only).
2. CURVE-A2: per-instance trim classifier + strategy routing (JudgmentEngine).
3. CURVE-A3: planar-on-surface contour extraction prototype (for plane×torus and similar).
4. CURVE-A4: analytic snap detectors (line/circle/ellipse first, then BSpline policy).
5. CURVE-A5: topology integration for tiered trims (still no STEP extension required).
6. CURVE-A6: STEP lane audit/prototype for intersection/curve-on-surface entities.
7. CURVE-A7: selectively enable exact export paths where representation+semantics are proven.

---

## 10) Recommended immediate next milestone

## **CURVE-A1 recommendation: Option A — tiered trim representation design + types (internal only)**

Why this is smallest safe next step:
- does not force premature generic quartic kernel,
- does not alter current BRep/STEP behavior,
- preserves exact provenance for hard cases now,
- creates a clean bridge for future classifier + snap + exporter work.

