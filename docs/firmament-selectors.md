# Firmament Selector Contracts + Primitive Semantic Naming (current v1 reality)

This document is the source-of-truth summary for the selector/semantic naming surface that is implemented **today**.

It answers two practical questions:

1. which primitive/feature semantic names are legal (`feature.port`), and
2. what those names currently mean in executable placement/selection behavior.

---

## Selector shapes

Firmament currently accepts these target/anchor shapes:

- bare feature id: `feature_id`
- single-port selector: `feature_id.port`

There is no implemented selector chaining or multi-hop traversal.

---

## Primitive semantic names (ports)

The table below documents the current contract ports for primitives involved in common authoring flows (including Wave 2.1 examples).

### `box`

- `top_face` → result kind `Face`, cardinality `One`
- `bottom_face` → result kind `Face`, cardinality `One`
- `side_faces` → result kind `FaceSet`, cardinality `Many`
- `edges` → result kind `EdgeSet`, cardinality `Many`
- `vertices` → result kind `VertexSet`, cardinality `Many`

Authoring note: box face naming is world-Z semantic (`top_face` means positive-Z planar face, `bottom_face` negative-Z planar face), not arbitrary CAD-face ordering.

### `cylinder`

- `top_face` → `Face`, `One`
- `bottom_face` → `Face`, `One`
- `side_face` → `Face`, `One`
- `circular_edges` → `EdgeSet`, `Many`
- `edges` → `EdgeSet`, `Many`
- `vertices` → `VertexSet`, `Many`

Current verified primitive-body topology for canonical cylinder bodies:

- `top_face` = 1 planar cap face
- `bottom_face` = 1 planar cap face
- `side_face` = 1 cylindrical face
- `circular_edges` = 2 circular cap edges
- `edges` = 3 total edges (2 circular + 1 seam)
- `vertices` = 4 total vertices

### `sphere`

- `surface` → `Face`, `One`

Current verified primitive-body topology for canonical sphere bodies:

- `surface` = 1 spherical face
- `edges` = 0
- `vertices` = 0

`top_face`/`bottom_face` are **not** legal sphere ports.

### `cone` (included for placement parity with cylinder)

- `top_face` → `Face`, `One`
- `bottom_face` → `Face`, `One`
- `side_face` → `Face`, `One`
- `circular_edges` → `EdgeSet`, `Many`
- `edges` → `EdgeSet`, `Many`
- `vertices` → `VertexSet`, `Many`

Pointed-cone cap selectors remain contract-legal but can resolve to zero runtime topology when that cap is absent.

---

## Boolean-feature semantic names

Current boolean outputs (`add`, `subtract`, `intersect`, plus bounded draft/chamfer/fillet outputs in selector contracts) share:

- `top_face` → `Face`, `One`
- `bottom_face` → `Face`, `One`
- `side_faces` → `FaceSet`, `Many`
- `edges` → `EdgeSet`, `Many`
- `vertices` → `VertexSet`, `Many`

---

## Placement-relevant meaning of names

For placement anchors (`place.on`, `place.on_face`, and compatibility alias `place.centered_on`) the resolver uses one shared anchor extraction path with representative geometry points and centroids:

- face/face-set ports → representative face points then centroid,
- edge-set ports → edge endpoint points then centroid,
- vertex-set ports → vertex points then centroid.

Important implication: `centered_on` is a compatibility alias of `on_face`, not a different mating solver. Canonical formatting normalizes alias usage to `on_face`.

---

## Around-axis selector rule (P1)

`place.around_axis` expects a selector that resolves a cylindrical/conical axis through `side_face`.

Canonical examples:

- `flange.side_face` where `flange` is a cylinder
- `cone_1.side_face` where `cone_1` is a cone

Using non-axis ports (for example `top_face`, `edges`, `vertices`) for `around_axis` is rejected.

---

## What is still not guaranteed

Even with semantic names, this is still a bounded selector system.

Not supported yet:

- selector chaining (`a.top_face.edges`)
- topology-stable persistent naming across arbitrary modeling edits
- generalized semantic traversal detached from current executed bodies

---

## Wave 2.1 canonical references

For concrete authoring of semantic placement with subtract tools:

- `testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament`
- `testdata/firmament/examples/w2_box_sphere_exterior_opening_pocket_semantic.firmament`
