# Primitive Definitions, Parameters, and Default Spatial Frames

This file is the spatial ground truth for primitive authoring.

Use it when you need to answer:
- where a primitive exists **before** placement,
- what point acts as its effective placement anchor,
- what its default world-space Z span is with no `place` block,
- how placement math composes with default-frame corrections.

## Common primitive rules

All primitive ops require:
- `op`
- `id` (non-empty scalar)

Optional:
- `place` block (legacy anchor+offset or semantic subset)

All feature IDs must be unique and referencable by later ops.

---

## Default-frame model used by execution

Primitive execution applies transforms in this order:

1. Create raw primitive body in kernel-local coordinates.
2. Apply primitive default local-frame publish transform (`box/cylinder/cone` only).
3. Apply placement translation from `FirmamentPlacementResolver`.

For primitives only, placement translation includes an extra local-frame correction for:
- `sphere`: `+radius` on Z
- `torus`: `+minor_radius` on Z

No primitive-local correction is applied in placement resolver for `box`, `cylinder`, or `cone`.

---

## `box`

### Required fields
- `size[3]` (numeric, each `> 0`)

### Raw body (before default frame)
`BrepPrimitives.CreateBox(sizeX,sizeY,sizeZ)` produces a box centered on origin:
- `x ∈ [-sizeX/2, +sizeX/2]`
- `y ∈ [-sizeY/2, +sizeY/2]`
- `z ∈ [-sizeZ/2, +sizeZ/2]`

### Default publish transform
Execution applies `+sizeZ/2` in Z.

### Final default span with no `place`
- `x ∈ [-sizeX/2, +sizeX/2]`
- `y ∈ [-sizeY/2, +sizeY/2]`
- `z ∈ [0, sizeZ]`

### Effective placement anchor intuition
Without placement, the bottom face lies on `z=0`. With selector-based placement on another top face, the box bottom lands on the anchor plane.

---

## `cylinder`

### Required fields
- `radius` (`> 0`)
- `height` (`> 0`)

### Raw body (before default frame)
`BrepPrimitives.CreateCylinder(radius,height)` is axis-aligned on Z and symmetric around `z=0`.

### Default publish transform
Execution applies `+height/2` in Z.

### Final default span with no `place`
- axis stays parallel to global Z
- bottom cap at `z=0`
- top cap at `z=height`

### Effective placement anchor intuition
Placement anchoring tends to align the cylinder bottom cap to the anchor Z when `offset[2]=0`.

---

## `cone`

### Required fields
- `bottom_radius` (`>= 0`)
- `top_radius` (`>= 0`)
- `height` (`> 0`)

### Constraints
- Radii cannot both be zero.
- If both radii are positive, they must be different (`bottom_radius != top_radius`).

### Raw body construction arithmetic
Cone creation performs two internal transforms:
1. Revolve profile points `(bottom_radius,0)` → `(top_radius,height)` around Z.
2. Immediately translate by `-height/2` in Z inside `ExecuteCone`.

Then primitive publish applies `+height/2` in Z (`ApplyDefaultLocalFrame`).

These two shifts cancel exactly.

### Final default span with no `place`
Cone cap plane origins land at:
- lower cap: `z=0`
- upper cap: `z=height`

This holds for frustum and pointed variants (one cap may be absent when a radius is zero, but span arithmetic still uses `[0,height]`).

### Effective placement anchor intuition
Cone behaves like box/cylinder for vertical anchoring in default frame: placement on a top face with zero offset starts its lower extent at anchor Z.

---

## `sphere`

### Required fields
- `radius` (`> 0`)

### Raw body (before default frame)
`BrepPrimitives.CreateSphere(radius)` is centered at origin.

### Default publish transform
No shift in `ApplyDefaultLocalFrame`.

### Placement resolver correction (primitive-specific)
Primitive placement translation adds `+radius` in Z.

### Final default span with no `place`
With no `place`, translation resolver returns zero (no placement block), so sphere remains centered at origin:
- center `(0,0,0)`
- `z ∈ [-radius,+radius]`

### Important placement consequence
When a `place` block exists (even zero offset), sphere gets the extra `+radius` Z correction.
Example: `on: base.top_face` at `z=4`, `radius=3`, zero offset ⇒ center at `z=7` (tangent contact), not `z=4`.

---

## `torus`

### Required fields
- `major_radius` (`> 0`)
- `minor_radius` (`> 0`)
- `major_radius > minor_radius`

### Raw body (before default frame)
`BrepPrimitives.CreateTorus(major,minor)` is centered at origin, axis along Z.

### Default publish transform
No shift in `ApplyDefaultLocalFrame`.

### Placement resolver correction (primitive-specific)
Primitive placement translation adds `+minor_radius` in Z.

### Final default span with no `place`
With no `place`, torus remains centered at origin:
- center `(0,0,0)`
- vertical span `z ∈ [-minor_radius,+minor_radius]`

### Important placement consequence
When a `place` block exists, torus gets the extra `+minor_radius` correction.
Example: anchor on `z=4`, `minor_radius=2`, zero offset ⇒ center at `z=6`.

---

## Selector-port contracts by feature kind

These are compile-time contracts used by placement and validation target checks.

- Box: `top_face`, `bottom_face`, `side_faces`, `edges`, `vertices`
- Cylinder: `top_face`, `bottom_face`, `side_face`, `circular_edges`, `edges`, `vertices`
- Cone: `top_face`, `bottom_face`, `side_face`, `circular_edges`, `edges`, `vertices`
- Sphere: `surface`
- Torus: `surface`, `edges`, `vertices`
- Boolean results (`add`/`subtract`/`intersect`): `top_face`, `bottom_face`, `side_faces`, `edges`, `vertices`

Boolean result ports are intentionally box-like, not primitive-tool-like.
