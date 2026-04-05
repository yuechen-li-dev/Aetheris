# File Shape, Sections, and Canonical Authoring Form

## Canonical source form

Repository corpus style is TOON-style indentation syntax in `.firmament` files.
JSON is parser-compatible but non-canonical for corpus authoring.

## Required top-level sections

Required:

- `firmament` (object-like)
- `model` (object-like)
- `ops` (array-like)

Optional:

- `schema` (object-like)
- `pmi` (array-like)

Unknown top-level sections are rejected.

## Required top-level fields

- `firmament.version` required.
- `model.name` required.
- `model.units` required.

## Ops structure

- `ops[n]:` header defines count in canonical TOON style.
- Each entry begins with `-` and contains an object-like block including `op`.
- `op` token must map to known op kind.

## Known op kinds (current)

Primitive:

- `box`, `cylinder`, `cone`, `torus`, `sphere`

Boolean:

- `add`, `subtract`, `intersect`

Validation:

- `expect_exists`, `expect_selectable`, `expect_manifold`

Pattern:

- `pattern_linear`, `pattern_circular`

## Section ordering note

Human docs recommend fixed section order (`firmament`, `model`, optional `schema`, `ops`, optional `pmi`).
Current parser enforces section presence/shape but not strict relative order; do not rely on relaxed behavior for canonical corpus.

## Schema block (current validation semantics)

When `schema` is present:

- `process` required and must be one of: `cnc`, `injection_molded`, `additive`.
- `cnc` requires `minimum_tool_radius > 0`.
- `injection_molded` requires:
  - `parting_plane` in `xy|yz|xz`
  - `gate_location` object-like with numeric `x,y,z`
  - `draft_angle > 0`
- `additive` requires `printer_resolution > 0`.

Additional current behavior:

- CNC DFM currently checks subtract-tool cylinder `with.radius >= minimum_tool_radius`.
- Enclosed voids are rejected for non-additive process modes.
