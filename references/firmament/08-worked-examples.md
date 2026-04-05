# Worked Examples (Canonical, LLM-Oriented)

These examples are intentionally small and semantically explicit.
Use them as templates for generation.

## 1) Primitive example: box default frame

```toon
firmament:
  version: 1

model:
  name: ex_box_default
  units: mm

ops[1]:
  -
    op: box
    id: base
    size[3]:
      10
      20
      30
```

Expected default extent (no placement):
- `x ∈ [-5, +5]`
- `y ∈ [-10, +10]`
- `z ∈ [0, 30]`

---

## 2) Placement example: sphere tangent to top face

```toon
firmament:
  version: 1

model:
  name: ex_sphere_contact
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      8
      8
      4
  -
    op: sphere
    id: ball
    radius: 3
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
```

Why center is `z=7` (not `z=4`):
- anchor from `base.top_face` gives `z=4`
- sphere placement adds primitive correction `+radius = +3`
- final center `z=7`

---

## 3) Boolean example: simple subtract family

```toon
firmament:
  version: 1

model:
  name: ex_box_hole
  units: mm

ops[2]:
  -
    op: box
    id: block
    size[3]:
      30
      20
      10
  -
    op: subtract
    id: hole
    from: block
    with:
      op: cylinder
      radius: 4
      height: 20
    place:
      on: block.top_face
      offset[3]:
        0
        0
        0
```

Notes:
- `from` chooses boolean source body.
- `place.on` chooses anchor source for translation.
- In some paths this placement is applied to tool before boolean; in others, result translation occurs after boolean.

---

## 4) Pattern example: linear chained subtracts

```toon
firmament:
  version: 1

model:
  name: ex_linear_pattern
  units: mm

ops[3]:
  -
    op: box
    id: plate
    size[3]:
      60
      20
      8
  -
    op: subtract
    id: hole_seed
    from: plate
    with:
      op: cylinder
      radius: 2
      height: 20
    place:
      on: plate.top_face
      offset[3]:
        -20
        0
        0
  -
    op: pattern_linear
    source: hole_seed
    count: 3
    step[3]:
      10
      0
      0
```

Expansion intent:
- generates `hole_seed__lin1`, `hole_seed__lin2`, `hole_seed__lin3`
- each instance adds `i * [10,0,0]` world offset to source placement
- each generated subtract chains from previous generated feature

---

## 5) Validation + export example

```toon
firmament:
  version: 1

model:
  name: ex_validation_export
  units: mm

ops[5]:
  -
    op: box
    id: base
    size[3]:
      20
      20
      10
  -
    op: subtract
    id: hole
    from: base
    with:
      op: cylinder
      radius: 3
      height: 20
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
  -
    op: expect_exists
    target: hole.top_face
  -
    op: expect_selectable
    target: hole.edges
    count: 4
  -
    op: expect_manifold
    target: hole
```

Interpretation:
- `expect_exists` selector target is allowed.
- `expect_selectable` requires selector target + integer count.
- `expect_manifold` requires bare feature ID target.
- Export body remains `hole` because it is the last executed geometric body (validation ops do not replace export selection).

---

## 6) Circular pattern example with synthesized target in validation

```toon
firmament:
  version: 1

model:
  name: ex_circular_pattern
  units: mm

ops[5]:
  -
    op: cylinder
    id: flange
    radius: 30
    height: 8
  -
    op: cylinder
    id: axis_ref
    radius: 1
    height: 20
  -
    op: subtract
    id: bolt_seed
    from: flange
    with:
      op: cylinder
      radius: 2
      height: 20
    place:
      on_face: flange.top_face
      around_axis: axis_ref.side_face
      radial_offset: 20
      angle_degrees: 0
      offset[3]:
        0
        0
        0
  -
    op: pattern_circular
    source: bolt_seed
    count: 5
    axis: axis_ref.side_face
    angle_degrees: 300
  -
    op: expect_manifold
    target: bolt_seed__cir5
```

This demonstrates targeting a synthesized pattern-generated feature ID in later validation.


## 7) Sphere placement edge case: no `place` vs `place.on: origin`

```toon
firmament:
  version: 1

model:
  name: ex_sphere_place_origin_difference
  units: mm

ops[2]:
  -
    op: sphere
    id: s_no_place
    radius: 3
  -
    op: sphere
    id: s_with_place
    radius: 3
    place:
      on: origin
      offset[3]:
        0
        0
        0
```

Key difference:
- `s_no_place` center is at `z=0`.
- `s_with_place` center is at `z=3` because any `place` block activates sphere `+radius` correction.

---

## 8) Linear pattern accumulation with non-zero source offset

```toon
firmament:
  version: 1

model:
  name: ex_linear_offset_accumulation
  units: mm

ops[3]:
  -
    op: box
    id: plate
    size[3]:
      80
      20
      8
  -
    op: subtract
    id: seed
    from: plate
    with:
      op: cylinder
      radius: 2
      height: 20
    place:
      on: plate.top_face
      offset[3]:
        5
        0
        0
  -
    op: pattern_linear
    source: seed
    count: 3
    step[3]:
      10
      0
      0
```

Generated offsets are cumulative from source offset:
- `seed__lin1`: `[15,0,0]`
- `seed__lin2`: `[25,0,0]`
- `seed__lin3`: `[35,0,0]`

---

## 9) Circular pattern angle composition with non-zero base angle

```toon
firmament:
  version: 1

model:
  name: ex_circular_angle_composition
  units: mm

ops[4]:
  -
    op: cylinder
    id: flange
    radius: 30
    height: 8
  -
    op: cylinder
    id: axis_ref
    radius: 1
    height: 20
  -
    op: subtract
    id: seed
    from: flange
    with:
      op: cylinder
      radius: 2
      height: 20
    place:
      on_face: flange.top_face
      around_axis: axis_ref.side_face
      radial_offset: 20
      angle_degrees: 30
      offset[3]:
        0
        0
        0
  -
    op: pattern_circular
    source: seed
    count: 3
    axis: axis_ref.side_face
    angle_step_degrees: 45
```

Generated instance angles:
- `seed__cir1`: `75°`
- `seed__cir2`: `120°`
- `seed__cir3`: `165°`

Pattern composes on top of base angle; it does not reset to zero.
