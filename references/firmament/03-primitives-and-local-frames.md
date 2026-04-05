# Primitive Definitions, Parameters, and Local Frames

This section defines each currently supported primitive, required fields, constraints, and default spatial frame behavior.

## Common primitive rules

All primitives require:

- `op`
- `id` (non-empty scalar)

Optional:

- `place` block (legacy or semantic placement)

## `box`

Fields:

- `size[3]` required; all components numeric and `> 0`.

Semantics:

- Dimensions interpreted as X, Y, Z extents.
- Underlying primitive is created at origin then translated by `+sizeZ/2` in Z as default local-frame publish transform.
- Published default body occupies approximately `z ∈ [0, sizeZ]` (before additional `place` translation).

## `cylinder`

Fields:

- `radius` required, numeric `> 0`
- `height` required, numeric `> 0`

Semantics:

- Axis is Z-axis in default construction.
- Underlying primitive then translated by `+height/2` in Z in publish path.
- Published default cylinder spans approximately `z ∈ [0, height]`.

## `cone`

Fields:

- `bottom_radius` required, numeric `>= 0`
- `top_radius` required, numeric `>= 0`
- `height` required, numeric `> 0`

Constraints:

- Not both radii zero.
- If both radii > 0, they must be unequal.

Semantics:

- Built via revolve profile along Z.
- Cone execution path recenters during construction then publish path applies `+height/2` Z shift.
- Published default cone occupies base/top along Z consistent with `z ∈ [0, height]` envelope.

## `sphere`

Fields:

- `radius` required, numeric `> 0`

Semantics:

- Raw sphere primitive is center-at-origin.
- Placement resolver applies extra primitive correction `+radius` in Z for primitive placement translation.
- Net published default center is elevated relative to origin unless placement counteracts it.

## `torus`

Fields:

- `major_radius` required, numeric `> 0`
- `minor_radius` required, numeric `> 0`
- constraint: `major_radius > minor_radius`

Semantics:

- Raw torus primitive centered at origin with axis along Z.
- Placement resolver applies extra primitive correction `+minor_radius` in Z for primitive placement translation.

## Selector-port contracts by primitive kind

- Box: `top_face`, `bottom_face`, `side_faces`, `edges`, `vertices`
- Cylinder: `top_face`, `bottom_face`, `side_face`, `circular_edges`, `edges`, `vertices`
- Cone: `top_face`, `bottom_face`, `side_face`, `circular_edges`, `edges`, `vertices`
- Sphere: `surface`
- Torus: `surface`, `edges`, `vertices`

These contracts are compile-time selector guards and influence placement/validation legality.
