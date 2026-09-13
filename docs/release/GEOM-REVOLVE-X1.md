# GEOM-REVOLVE-X1 — axis-defined bounded revolution

## Executive verdict

**Accepted for the documented bounded X1 Base lane.** Firmament can now express a standalone turned/revolved solid directly as `Profile + Axis + Angle`, without arbitrary Boolean authoring. Unsupported topology is rejected explicitly; it is not generalized by implication.

## Architecture audit

Aetheris already had one rotational geometry backend: `BrepRevolve`, analytic cylinder/cone/sphere/torus carriers, generic `SurfaceOfRevolutionSurface`, STEP `SURFACE_OF_REVOLUTION` import/export, and seam-aware display/import support. The authoritative public axis value remains `ConceptIrAxisValue`/exact Axis binding. Full periodic faces use representational seam topology; partial trims use bounded circular edges and real radial cap faces. Cylinder, cone/frustum, sphere, and torus are analytic special cases of revolution.

X1 adds a finite semantic binder and `RevolveBRepPlan`; it does not add a parser callback into BRep or a second sketch/Boolean engine. The Profile's `ConstructionPlane` and explicit Axis determine world placement and the zero-angle radial direction.

## Angle model and rejection

- dimensionless numeric values are radians;
- `90deg` is the canonical degree literal;
- `quarter`, `half`, and `full` are lowercase semantic aliases;
- signed angles select direction;
- zero and magnitudes greater than a full turn are fatal before materialization.

Typed diagnostics cover missing/zero/skew axes, zero/out-of-range angles, invalid/zero-area Profiles, and Profiles crossing the Axis. The nearby Profile validator now integrates circular-arc signed area exactly, closing the false zero-area gap exposed by a semicircle.

## Artifact evidence

Canonical witnesses live under `fixtures/Canonical/Revolve/`: sphere, stepped shaft, partial quarter, non-principal-axis ring, and knob. Invalid witnesses live under `fixtures/Invalid/Revolve/`.

- sphere: bounds `[-20,20]` on X/Y/Z, one exact `SPHERICAL_SURFACE`, enclosed STEP reimport;
- stepped shaft: bounds `[-20,20] x [-30,30] x [-20,20]`, four cylinders plus four planar shoulders, enclosed STEP reimport;
- partial quarter: bounds `[0,20] x [-10,10] x [-20,0]`, two cylinders, four planes including `Start`/`End`, enclosed STEP reimport;
- arbitrary axis: one body, six faces, exact plane/cylinder inventory, enclosed STEP reimport;
- knob: piecewise-linear stem/head section with analytic cylinders/cones/planes and no faceted fallback.

Equivalent `full` and `360deg` inputs produce byte-identical STEP in focused tests. `inspect --json` reports radians, degrees, alias, direction, full/partial classification, Axis, and Profile frame.

## Explicit boundary

The admitted X1 body has one outer line/arc loop. Semicircle-about-diameter is the proven axis-touching sphere lane; other axis-touching topology and inner loops are deferred. X1 exposes Base only. Add/Remove composition, sheet revolve, multi-turn, Thread/helix, skew projection, and arbitrary Boolean operations remain unsupported. Cylinder/Sphere/Cone syntax remains preferred when that primitive is the engineering intent.
