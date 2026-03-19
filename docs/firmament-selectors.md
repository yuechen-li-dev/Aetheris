# Firmament Selector Contracts

This document describes the selector surface that is implemented today.

## Selector shapes

Firmament currently accepts these target/anchor shapes:

- bare feature id: `feature_id`
- single-port selector: `feature_id.port`

There is no implemented selector chaining or multi-hop traversal.

## Contract-level selector ports

### Primitive features

#### `box`

- `top_face` → result kind `Face`, cardinality `One`
- `bottom_face` → result kind `Face`, cardinality `One`
- `side_faces` → result kind `FaceSet`, cardinality `Many`
- `edges` → result kind `EdgeSet`, cardinality `Many`
- `vertices` → result kind `VertexSet`, cardinality `Many`

#### `cylinder`

- `top_face` → `Face`, `One`
- `bottom_face` → `Face`, `One`
- `side_face` → `Face`, `One`
- `circular_edges` → `EdgeSet`, `Many`
- `edges` → `EdgeSet`, `Many`
- `vertices` → `VertexSet`, `Many`

#### `sphere`

- `surface` → `Face`, `One`
- `edges` → `EdgeSet`, `Many`
- `vertices` → `VertexSet`, `Many`

### Boolean features

Current boolean roots (`add`, `subtract`, `intersect`) share this contract surface:

- `top_face` → `Face`, `One`
- `bottom_face` → `Face`, `One`
- `side_faces` → `FaceSet`, `Many`
- `edges` → `EdgeSet`, `Many`
- `vertices` → `VertexSet`, `Many`

## Result kinds and cardinality

The current selector contract metadata distinguishes:

- result kinds: `Face`, `FaceSet`, `EdgeSet`, `VertexSet`
- cardinality: `One`, `Many`

These are attached at compile time for legal selector-shaped targets.

## What is topology-backed today

Selectors are still intentionally narrow.

Today they are backed by current runtime/body topology checks such as:

- face count
- edge count
- vertex count
- placement-anchor extraction from representative face/edge/vertex points

This is enough for current validation and placement flows, but it is not a generalized semantic selector system.

## Not supported yet

The current implementation does **not** support:

- selector chaining such as `a.top_face.edges`
- multi-hop traversal across derived topology
- richer topology naming guarantees beyond current feature contracts and runtime checks
- arbitrary semantic resolution detached from current executed bodies

Selectors must start from a source feature id, which remains the language-stable identity.
