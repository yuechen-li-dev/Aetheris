# WIRE-X1 — Coil / SurfaceCoil foundation

## Executive verdict

**Meaningful progression.** Aetheris now authors `AxisCoil` and authored-support `SurfaceCoil` operations, retains exact/evaluable winding laws in WireForm AIR, derives terminals and stock accounting, rejects invalid offsets/poles/turn overlap, and exports deterministic enclosed non-rational cubic B-spline STEP tubes that reimport as manifolds. Cylinder, frustum, and bounded sphere winding are working through the real CLI path. The remaining acceptance blocker is the flagship assembly evidence: the Blender-ball source contains semantic frustum and sphere references, but the current ordinary WireForm exporter selects the coil body only. It does not yet emit those two reference bodies in the same AP242 product structure or report measured coil-to-ball distance. The artifact is therefore an honest coil witness, not yet the requested three-body qualification assembly.

## Architecture and audit

```text
WireForm
  → Straight / Bend / AxisCoil / SurfaceCoil semantic operation
  → exact line/arc or exact/evaluable winding law
  → Centerline AIR authority
  → deterministic bounded cubic non-rational approximation (coil only)
  → rotation-minimal circular-section transport
  → analytic cylinder/torus or polynomial B-spline tube
  → enclosed BRep / AP242 STEP
```

The generalized abstraction is `WireFormOperationAir` plus an evaluable `WireCoilAir`. `WireStraightAir` and `WireBendAir` remain special state transitions and retain exact cylinder/torus realization whenever no coil is present. Generated winding laws feed a new circular tube realization; samples are validation/materialization evidence rather than semantic authority. Existing analytic Cylinder, Cone/Frustum, and Sphere declarations provide stable authored support identities. Existing B-spline curve/surface evaluators and STEP support are reused. Lines/arcs and support descriptions remain exact/analytic; a product STEP helix and its swept tube become bounded degree-3 non-rational polynomial patches.

## Semantic and qualification evidence

- Axis flagship: 2 mm wire, radius 12 mm, pitch 5 mm, 8 turns, height 40 mm, right-handed. Exact centerline length is 604.510625748 mm; ideal volume is 1899.12614087 mm³; stainless mass is 0.0150030965 kg; minimum turn clearance is 3 mm.
- Frustum flagship: authored 18/30 mm radii × 60 mm support, Inside, 1 mm requested/measured analytic support clearance, six turns at 8 mm axial pitch. Derived path length is 787.154196452 mm and sampled conservative turn clearance evidence is 6.136653 mm.
- Sphere flagship: authored 24 mm sphere, Outside, 1 mm clearance, six turns, +55° to −55°. Progression is linear in latitude, not equal-geodesic; exact poles are rejected. Derived path length is 837.952465425 mm and measured turn clearance is 6.265152 mm.
- Blender-ball source: authored 20/34 × 64 mm cup reference plus a seven-turn spherical winding around the 9 mm captive-ball proxy, with 1 mm outside clearance and a +65° to −65° latitude-linear span. Coil path length is 387.357156248 mm and turn clearance is 1.534378 mm. The support references remain semantic declarations in the source but are not yet included as separate bodies in the emitted single-body STEP.

Coil STEP inventory is planar terminal caps plus degree-3 non-rational B-spline surfaces/curves, with zero rational product surfaces and zero faceted fallback. All four coil bodies reimport as enclosed manifolds. The deterministic realization uses 32 longitudinal cubic spans per turn and four cubic polynomial quarter-section patches. Measured maximum/RMS centerline errors are 0.000365/0.000308 mm (Axis), 0.001688/0.001162 mm (frustum), 0.005180/0.002762 mm (sphere), and 0.002449/0.001337 mm (spherical Blender coil), all below the 0.01 mm acceptance tolerance. Identical Axis builds have matching SHA-256 `35974623f6da79fbf1956de1bbae35da4f562cc5c0272c7d19651da6681bd5cb`.

## Validation and regressions

Focused coverage includes Turns/Pitch/Height resolution, handedness, analytic helix length, terminal state composition, cylinder/AxisCoil equivalence, frustum and sphere support laws, pole/offset/support diagnostics, turn overlap, non-rational topology, structured CLI inspection, STEP reimport, deterministic exports, and the complete WIRE-X0 WireForm test class. Coil-free Paperclip remains on the original exact path: 4 cylinders, 3 tori, 2 caps, and 0 B-splines. The warning-free Release solution build passed, as did the full serial repository suite (3,225 tests; no failures or skips; the intentionally empty FrictionLab test assembly still has no discoverable tests), fixture/layout/Markdown coverage within that suite, wireframe rendering for iso/front views, and `git diff --check`.

Generated manual artifacts live under ignored `artifacts/local/`: the requested Axis, frustum, sphere, and Blender-ball coil STEP files plus iso/front wireframe SVGs. No spring stiffness, dynamics, mixing performance, or contact mechanics are claimed.
