# Firmament Language Shape

This document defines the **canonical source shape** of `.firmament` files used by the Aetheris Firmament DSL.

All `.firmament` source files in the repository are written using this structure.

The syntax style is **TOON-shaped indentation syntax**, not JSON.

---

# Minimal Valid Firmament File

```
firmament:
  version: 1

model:
  name: demo
  units: mm

ops[0]:
```
---

# Top-Level File Structure

Every `.firmament` file follows this section order:
```
firmament:
  version: 1

model:
  name: demo
  units: mm

schema:
  process: cnc

ops[0]:

pmi[0]:
```
Rules:

1. `firmament`, `model`, and `ops` are required.
2. `schema` and `pmi` are optional.
3. Section order is fixed.
4. Unknown top-level sections are not allowed.
5. All indentation is two spaces.
6. Tabs must never be used.

---

# Canonical Empty Arrays

Empty arrays must use **explicit array-length syntax**.

### Correct

`ops[0]:`

`pmi[0]:`

### Incorrect

`ops: []`

`ops:`

`pmi: []`

---

# Canonical Primitive Operation

Example primitive feature:

```
ops[1]:
  -
    op: box
    id: base_plate
    size[3]:
      100
      50
      10
```

Rules:

- `op` defines the operation type
- `id` names the feature
- primitive parameters use explicit array-length syntax

---

# Canonical Multiple Operations

Example with two operations:
```
ops[2]:
  -
    op: box
    id: b1
    size[3]:
      100
      50
      10

  -
    op: subtract
    id: cut1
    from: b1
    with:
      op: cylinder

```
Rules:

- Each op entry begins with `-`
- Ops are ordered operations
- Boolean ops reference earlier features

---

# Primitive Operation Shapes

## box
```
op: box
id: base
size[3]:
  100
  50
  10
```
## cylinder
```
op: cylinder
id: hole
radius: 5
height: 20
```
## sphere
```
op: sphere
id: ball
radius: 12
```
---

# Boolean Operation Shapes

## add
```
op: add
id: join1
to: base
with:
  op: cylinder
```
## subtract
```
op: subtract
id: cut1
from: base
with:
  op: cylinder
```
## intersect
```
op: intersect
id: clip1
left: base
with:
  op: sphere
```
Notes:

- `with` contains the tool operation
- nested ops are treated as tool geometry

---

# Placement Shape (P1 subset)

Placement remains optional. When present, use a small explicit `place` object.

Legacy anchor placement:

```
place:
  on: origin
  offset[3]:
    0
    0
    0
```

Semantic placement subset (P1):

```
place:
  on_face: base.top_face
  centered_on: flange.top_face
  around_axis: flange.side_face
  radial_offset: 20
  angle_degrees: 30
  offset[3]:
    0
    0
    -12
```

Rules:
- Supported semantic fields are exactly: `on_face`, `centered_on`, `around_axis`, `radial_offset`, `angle_degrees`.
- `radial_offset` requires `around_axis`.
- Unknown placement semantic keys are rejected.
- This is a minimal ergonomics subset; it is **not** a general frame/constraint language.

---

# Validation Operations

Validation operations do not create geometry.

## expect_exists
```
op: expect_exists
target: base
```
## expect_selectable
```
op: expect_selectable
target: hole
count: 4
```
## expect_manifold
```
op: expect_manifold
```
---

# Scalar Style

Use simple scalars wherever possible.

### Correct
```
version: 1
units: mm
radius: 10
```
### Avoid unnecessary quoting

Incorrect:

`units: "mm"`
`version: "1"`

---

# Selectors

Selectors reference previously created features.

Format:

`feature_id`
`feature_id.sub_element`

Examples:

`base`
`base.top_face`
`hole.entry_face`

Selector resolution is handled by the compiler.

---

# Forbidden Source Forms

`.firmament` source files must **never** be written as JSON.

Example of forbidden format:
```
{
  "firmament": { "version": "1" },
  "model": { "name": "demo", "units": "mm" },
  "ops": []
}
```
Firmament source is always written using canonical indentation syntax.

---

# Repository Canonical Sources

All `.firmament` fixtures under:

`testdata/firmament/fixtures/`

must follow this canonical format exactly.

These fixtures serve as **authoritative language examples**.
