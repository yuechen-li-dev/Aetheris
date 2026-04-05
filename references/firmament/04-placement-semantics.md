# Placement Semantics (Deep Reference)

Placement is represented by `place` block and parsed into:

- legacy anchor: `on`, `offset`
- semantic subset: `on_face`, `centered_on`, `around_axis`, `radial_offset`, `angle_degrees`

Unknown keys are rejected.

## `place.on`

Accepted anchors:

- `origin`
- selector-shaped token `feature.port`

Meaning:

- establishes anchor point source unless semantic anchors are provided.

Runtime anchor extraction from selector:

- face/face-set ports: representative face points, centroided.
- edge-set ports: edge endpoint point cloud centroid.
- vertex-set ports: vertex point cloud centroid.

## `place.offset`

- Expected exactly 3 numeric components.
- For legacy placement (no semantic fields), `on` and `offset` are both required by validators.
- At runtime, missing/invalid offset list resolves to zero vector.

## `place.on_face`

- Selector-shaped semantic anchor.
- If present, preferred over `centered_on` and `on` for base anchor resolution.
- Semantically acts as a selector-derived anchor point, not full mating/alignment.

## `place.centered_on`

- Selector-shaped semantic anchor.
- Used if `on_face` absent.
- Currently same anchor-point extraction mechanics as `on_face` (centroid-based), i.e., no richer alignment behavior.

## `place.around_axis`

- Selector-shaped axis source.
- Runtime axis resolution currently expects `side_face` on cylindrical/conical geometry.
- If neither `on_face`/`centered_on`/`on` is present, base anchor defaults to axis origin.

## `place.radial_offset`

- Numeric scalar radius from axis.
- Requires `around_axis` (validated).
- Defaults to 0 when omitted while `around_axis` present.

## `place.angle_degrees`

- Numeric scalar angular parameter in degrees.
- Used with around-axis radial basis (`u/v`) generated from world reference vectors.
- Defaults to 0 when omitted.

## Composition order inside placement resolver

For primitives and booleans:

1. Resolve semantic anchor point (`on_face` → `centered_on` → `on` → axis-origin fallback → origin fallback).
2. If `around_axis` provided, transform anchor to radial point using `radial_offset` + `angle_degrees`.
3. Add raw `offset` vector.
4. For primitives only: add primitive-specific local-frame correction (`sphere:+radius Z`, `torus:+minorRadius Z`).

## Primitive execution placement application

Primitive publish flow:

1. Build raw primitive body.
2. Apply default local frame publish shift (`box/cylinder/cone` +Z half-height).
3. Apply placement translation computed above.

This means final published primitive position includes both default-frame shift and placement translation.

## Boolean placement application nuances

Boolean placement may apply differently depending on path:

- In semantic tool-placement path (notably some subtract-cylinder cases), placement is applied to tool before boolean.
- Otherwise boolean runs with unplaced tool, and resulting boolean body is translated afterward.

This distinction is behaviorally significant and documented as a mismatch/gotcha in `07-semantic-mismatches-and-gotchas.md`.
