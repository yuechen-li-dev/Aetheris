# CIR-F8: guided analytic materialization capability audit for sphere/cone/torus subtract families

## Outcome

**Outcome B — partial architecture with blockers.**

Aetheris already has broad analytic *surface* representation across Firmament/CIR/BRep/STEP, but exact replay-guided rematerialization is still constrained by (1) intersection/trim-curve generation and (2) robust face-loop/topology assembly for non-planar trimmed patches. A surface-family architecture is viable, but only if introduced with a trim capability matrix and patch descriptors first.

## 1) Layer support matrix

| Family | Firmament primitive/tool | CIR node/tape | BRep primitive | BRep surface kind | STEP AP242 export | Boolean/materializer support |
|---|---|---|---|---|---|---|
| Plane / box | Yes (`box`) | Yes (`CirBoxNode`, `EvalBox`) | Yes | Plane | Yes (`PLANE`) | Strong: `subtract(box,box)`, many safe composition paths |
| Cylinder | Yes (`cylinder`) | Yes (`CirCylinderNode`, `EvalCylinder`) | Yes | Cylinder | Yes (`CYLINDRICAL_SURFACE`) | Strong: `subtract(box,cylinder)` plus safe-family boolean builders |
| Sphere | Yes (`sphere`) | Yes (`CirSphereNode`, `EvalSphere`) | Yes | Sphere | Yes (`SPHERICAL_SURFACE`) | No dedicated CIR rematerializer strategy yet |
| Cone | Yes (`cone`) | **Partial**: local-frame shift exists, but no cone CIR primitive/tool lowering path | Yes | Cone | Yes (`CONICAL_SURFACE`) | No dedicated CIR rematerializer strategy |
| Torus | Yes (`torus`) | Yes (`CirTorusNode`, `EvalTorus`) | Yes | Torus | Yes (`TOROIDAL_SURFACE`) | Explicitly recognized unsupported strategy: `subtract(box,torus)` |
| Prismatic/extrusion | Yes (`triangular_prism`, `hexagonal_prism`, `straight_slot`, `slot_cut`) | No general extrusion CIR node family | Yes (`BrepExtrude`-backed primitives) | Mostly planar side/cap surfaces | Exported through bound topology + line/arc/spline edges | Safe boolean families exist for polygonal/prismatic through-cuts |
| BSpline/NURBS | Not central in Firmament primitives | Not central in CIR booleans | Present in geometry model | `BSplineSurfaceWithKnots` | Yes (`B_SPLINE_SURFACE_WITH_KNOTS`) | Not in current CIR rematerializer families |

## 2) Curve/trim capability matrix

### Current curve representation/export

| Curve type | BRep representation | STEP export | Topology edge/coedge use | Notes |
|---|---|---|---|---|
| Line | Yes (`Line3Curve`) | Yes (`LINE`) | Standard edge binding + trim interval | Mature |
| Circle | Yes (`Circle3Curve`) | Yes (`CIRCLE`) | Standard edge binding + trim interval | Mature |
| Ellipse | Yes (`Ellipse3Curve`) | Yes (`ELLIPSE`) | Standard edge binding + trim interval | Present but less exercised than line/circle |
| Polyline | Indirect (as many line edges, e.g., prism profiles) | As multiple `LINE` edge curves | Fully supported via multi-edge loops | No single polyline curve entity |
| BSpline | Yes (`BSpline3Curve`) | Yes (`B_SPLINE_CURVE_WITH_KNOTS`) | Standard edge binding + trim interval | Available but not yet core to CIR boolean rematerialization |
| Implicit/algebraic (quartic etc.) | No first-class analytic implicit curve entity | No direct export path today | Would need approximation or new representation | Major blocker for torus/general quadric intersections |

### What intersections are realistically representable now

- **Plane × plane → line:** representable now.
- **Plane × cylinder:** line/circle/ellipse special cases representable now.
- **Plane × sphere:** circle representable now.
- **Plane × cone:** conic sections may need hyperbola/parabola forms not first-class; limited safe subcases (circle/ellipse-like) are easier.
- **Plane × torus:** generally quartic; not representable as current first-class trim curve kinds.
- **Cylinder × cylinder / sphere × cylinder / torus × anything:** only special aligned cases that collapse to line/circle/ellipse are tractable with current curve model.

## 3) Barriers for `subtract(box, sphere)`

- **Sphere CIR support:** yes.
- **Sphere BRep surface:** yes.
- **STEP spherical face export:** yes.
- **Topology can host trimmed spherical patches:** yes in principle (faces/loops/coedges are generic), but construction logic for robust patch segmentation is missing.
- **Trim curves:** box-plane ∩ sphere gives circles; curve model can represent circles.
- **Main blockers:** robust intersection solving, split/loop stitching, and stable face-orientation/provenance assembly.
- **Safe near-term subcases:** axis-aligned centered sphere where each active cut loop is circular and can be mapped cleanly.
- **Harder subcases:** translated/rotated placements yielding multiple interacting spherical patches and complex loop ownership.

## 4) Barriers for `subtract(box, cone)`

- **Cone support layers:** Firmament+BRep+STEP yes; CIR lowering for cone booleans is currently incomplete.
- **Trims:** plane-cone intersections are general conics; current curve set lacks explicit parabola/hyperbola entities.
- **Representability:** circular/elliptic special cases may be possible; general cases are blocked.
- **Caps/floor/side surfaces:** representable as planar + conical faces, but dependable trim generation/classification is missing.
- **Net:** feasible only for tightly constrained cone orientations/angles unless curve model expands.

## 5) Barriers for `subtract(box, torus)`

- **Torus support layers:** Firmament/CIR/BRep/STEP all exist.
- **Current materialization contract:** replay-guided `subtract(box,torus)` is intentionally recognized then rejected with explicit unsupported diagnostic.
- **Trims:** plane-torus intersection is generally quartic; no first-class representation today.
- **Restricted orientations:** some slices may degenerate to circles/paired circles, but a reliable classifier and subcase partitioner do not yet exist.
- **Blunt assessment:** near-term exact analytic torus subtract rematerialization is not realistic without substantial curve/intersection infrastructure. Torus is currently a high-risk trap for architecture overreach.

## 6) Surface-family materializer architecture viability

A `SurfaceFamilyMaterializer` architecture is viable **if** used behind a planner and capability checks, not as unconditional replacement.

Candidate handlers and readiness:

- `PlanarSurfaceMaterializer` — can exist now.
- `CylindricalSurfaceMaterializer` — can exist now for known-safe trim types.
- `SphericalSurfaceMaterializer` — plausible now for restricted circle-trim families.
- `ConicalSurfaceMaterializer` — needs cone CIR coverage + conic trim policy first.
- `ToroidalSurfaceMaterializer` — should exist only as capability gate/diagnostic initially.
- `PrismaticExtrusionMaterializer` — can leverage existing polygonal/prismatic safe paths.

Why this is better than pair-specific explosion:

- decouples **surface emission** from **boolean pair naming**;
- allows reusable trim validation across subtract/union/intersect;
- keeps existing pair-specific strategies as fast paths while enabling gradual generalization.

Replay log + CIR provenance role:

- Replay log identifies intended operation/tool/placement lineage.
- CIR tree provides normalized geometric intent and transform context.
- Together they seed descriptor generation and admissibility checks before any topology build.

## 7) Proposed descriptor model

### SourceSurfaceDescriptor

```text
family
parameters
transform
provenance
owningCirNodeId / replayOpIndex / featureId
orientationRole (base/tool/cap/side/seam)
```

### TrimCurveDescriptor

```text
curveKind
parameters
sourceSurfacePair
parameterSpaceMap (optional)
domainInterval(s)
provenance
capabilityTag (exact/special-case/approx/deferred)
```

### FacePatchDescriptor

```text
sourceSurfaceRef
outerLoopDescriptors
innerLoopDescriptors
orientation
role/provenance
adjacencyHints
```

## 8) JudgmentEngine / planner role

Use `JudgmentEngine` as guided planner:

1. Generate strategy candidates:
   - existing pair-specific materializer fast paths;
   - surface-family assembly path;
   - explicit unsupported diagnostic strategy.
2. Evaluate admissibility from replay + CIR + capability matrix.
3. Score by certainty/specificity (exact pair fast path highest; constrained surface-family next; unsupported last).
4. Return selection + structured rejection reasons for auditability.

This preserves CIR fall-forward discipline while preventing hidden broadening.

## 9) Recommended implementation ladder

1. **CIR-F8.1:** introduce `SourceSurfaceDescriptor` extraction from replay+CIR (no behavior change).
2. **CIR-F8.2:** add centralized trim capability matrix (surface-pair × curve-kind × status).
3. **CIR-F8.3:** introduce `FacePatchDescriptor` and validation-only assembly dry-run.
4. **CIR-F9:** implement restricted spherical materializer path (e.g., safe `box-sphere` subset) behind planner.
5. **Later:** cone restricted subsets after cone CIR parity + conic policy.
6. **Defer:** torus exact materialization until quartic/advanced trim infrastructure exists.

## 10) Immediate next milestone

**Recommended:** `CIR-F8.1: source-surface descriptor + trim capability matrix`.

Reason: it is the smallest generalization step that unlocks family-based planning without changing public behavior, boolean semantics, or CIR fall-forward scope.
