# Placement Semantics (Anchor Frames, Offsets, and Timing)

This file defines what `place` means in executable terms.

## Placement shape

Supported placement keys:
- `on`
- `offset`
- `on_face`
- `centered_on`
- `around_axis`
- `radial_offset`
- `angle_degrees`

Unknown placement keys fail validation.

Legacy mode (no semantic keys used):
- `place.on` required
- `place.offset` required and must be exactly 3 numeric components

Semantic mode (any semantic key used):
- `place.on` can be omitted
- `offset` defaults to zero if omitted or malformed at runtime
- `radial_offset` requires `around_axis`

---

## Anchor resolution order

Base anchor point is resolved in this strict precedence:
1. `on_face` (selector)
2. `centered_on` (selector)
3. `on` (`origin` or selector)
4. if none above and `around_axis` exists: axis origin
5. otherwise: world origin `(0,0,0)`

`on_face` and `centered_on` currently use the same centroid-like anchor extraction logic. Names are ergonomic; behavior is not full mating/alignment.

---

## What selector anchors reference

For selector anchors (`feature.port`), runtime extracts representative points from published geometry of earlier features:

- Face/face-set ports: representative face points, then centroid.
- Edge-set ports: edge endpoint points, then centroid.
- Vertex-set ports: vertex points, then centroid.

Port legality is checked first by selector contracts, then runtime extraction heuristics apply.

---

## What `offset[3]` means

`offset` is a **world-space translation vector** `(dx,dy,dz)` added after anchor (and optional around-axis) resolution.

It is **not** measured in local face tangent coordinates, tool coordinates, or axis-relative polar frame.

So for `offset[3]: [1,-2,5]`, placement always adds +1 X, -2 Y, +5 Z in world axes.

---

## Around-axis semantics

When `around_axis` is present:

1. Resolve axis from selector (currently expects `side_face` on cylindrical/conical surfaces).
2. Project base anchor to that axis.
3. Build radial basis from world references.
4. Apply radial vector:
   - radius = `radial_offset` (default 0)
   - angle = `angle_degrees` (default 0)

This computes a new base point before `offset` is added.

---

## Primitive placement translation formula

For primitives, final placement translation vector is:

`(anchorPoint - origin) + offset + primitiveLocalCorrection`

where primitiveLocalCorrection is:
- sphere: `(0,0,radius)`
- torus: `(0,0,minor_radius)`
- others: `(0,0,0)`

Then execution applies this translation to the already default-framed primitive body.

Equivalent primitive pipeline:
1. create raw primitive
2. apply default-frame publish shift (`box/cylinder/cone` only)
3. apply placement translation formula above

---

## Boolean placement translation formula

For booleans, placement resolver returns:

`(anchorPoint - origin) + offset`

(no sphere/torus-style primitive correction for boolean feature placement)

### Timing nuance (critical)
Boolean placement is path-dependent in execution:

- **Semantic tool-placement path**: tool body is default-framed (for box/cylinder/cone tools), then translated, then boolean runs.
- **General path**: boolean runs with unplaced tool, then final boolean result body is translated.

These are geometrically different operations in many cases.
Do not assume `place` always means “move tool before boolean.”

---

## Anchor/reference distinction for booleans

Boolean body reference fields and placement anchors are separate semantics:

- `from` / `to` / `left` choose the primary geometry input to boolean op.
- `place.on` / `place.on_face` / `place.centered_on` choose where placement anchor is sampled.

They can reference different features.

---

## Practical authoring checklist

Before writing placed features, verify:
1. selector root is an earlier feature ID,
2. selector port is allowed for that feature kind,
3. `offset` is a 3-number world vector,
4. sphere/torus placement includes extra +Z correction,
5. boolean placement timing assumptions match supported execution path.
