# Firmament Placement Semantics (current executable rules)

This is the bounded, executable placement model in current Firmament.

## 1) Default placement (no `place` block)

If an op has no placement block, no placement translation is applied.

For primitives, note there is still primitive-specific default publish framing in execution (for example, box/cylinder/cone local bodies are shifted into the published frame before placement translation is considered).

---

## 2) Placement keys in one unified model

Supported keys:

- `on`
- `offset[3]`
- `on_face`
- `around_axis`
- `radial_offset`
- `angle_degrees`

Unified behavior:

- semantic anchor placement prefers `on_face`,
- explicit spatial fallback uses `on` + `offset[3]`,
- `centered_on` is a compatibility alias of `on_face`,
- if both `on_face` and `centered_on` are present they must match exactly,
- `radial_offset` requires `around_axis`.

---

## 3) Anchor resolution order

Base anchor point is resolved in this strict precedence:

1. canonical semantic anchor (`on_face`, or `centered_on` alias normalized to `on_face`)
2. `on` fallback anchor
3. if none above and `around_axis` exists: axis origin
4. otherwise world origin `(0,0,0)`

---

## 4) What `offset[3]` means

`offset[3]` is a world-space translation `(dx,dy,dz)`.

It is **not** a local face UV offset and **not** an axis-local vector.

---

## 5) Around-axis semantics

When `around_axis` is present:

1. resolve axis from selector (`side_face` on cylinder/cone),
2. project the current base anchor to that axis,
3. build radial basis from world references,
4. apply `radial_offset` (default `0`) and `angle_degrees` (default `0`),
5. then apply world `offset[3]`.

---

## 6) Primitive placement formula

Primitive translation is:

`(anchorPoint - origin) + offset + primitiveLocalCorrection`

Current correction terms:

- sphere: `(0, 0, radius)`
- torus: `(0, 0, minor_radius)`
- others: `(0, 0, 0)`

---

## 7) Boolean placement formula and timing

Boolean placement translation value is:

`(anchorPoint - origin) + offset`

But execution timing is important:

- semantic tool-placement path: tool is placed first, then boolean executes,
- general path: boolean executes first, then result is translated.

These are not equivalent in general.

Wave 2.1 subtract examples intentionally use semantic placement so placement directly controls tool location before subtraction.

---

## 8) Subtract-tool authoring for Wave 2.1

### Cylinder-root blind bore

Canonical pattern:

- root cylinder,
- one subtract cylinder tool,
- `place.on_face: <root>.top_face` with negative Z `offset` equal to blind depth.

Reference: `testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament`.

### Exterior-opening sphere pocket (box root)

Canonical pattern:

- subtract sphere from box,
- place sphere center near `top_face` using `place.on_face` + negative Z offset,
- ensure strict non-tangent intersection (avoid exactly tangent placement).

Reference: `testdata/firmament/examples/w2_box_sphere_exterior_opening_pocket_semantic.firmament`.

---

## 9) Common failure modes

- Using contained sphere placement when you intended an exterior-opening pocket.
- Tangent/grazing placement (zero-thickness boundary conditions) instead of strict overlap.
- Assuming `offset` is face-local; it is world XYZ.
- Authoring both `on_face` and `centered_on` with different selectors (now rejected as ambiguous).
