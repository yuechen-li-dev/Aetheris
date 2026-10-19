# Ordinary CAD edge-finish robustness

- `d-profile-fillet.firmament`: one semicircular arc and one chord, extruded
  8 mm, with a 1 mm top-rim fillet.
- `d-profile-chamfer.firmament`: the same stock with a 1 mm chamfer.
- `polygon-rim-fillet.firmament`: a parallelogram with acute/obtuse corners,
  finished without treating every junction as 90 degrees.

Sources use preferred field casing. They are accepted cases, not speculative
syntax or recorded refusals. They must emit the requested finish, not the
unchanged extrusion. D outputs have six faces, ten edges and six vertices;
their two caps are legitimate arc/chord digons.

`CircularSegmentEdgeFinishTests` proves analytic-support containment, shared
incidence, deterministic STEP/reimport, placement frames, both windings,
minor/major circular segments, and rejection of cap collapse. The fillet miter
is a certified finite curve within 1e-6 mm, while the chamfer miter is an exact
quadratic polynomial. `OrdinaryEdgeFinishRobustnessTests` covers polygon angles,
collinear subdivisions, bottom finishes and oversized insets. CLI tests exercise
validate/build agreement, named selections, feature identity and planner evidence.

Generated outputs belong under `artifacts/local/edge-finish-robustness/`.
