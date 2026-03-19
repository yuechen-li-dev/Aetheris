# Firmament Overview

Firmament is the Aetheris DSL for defining a small, ordered 3D feature program in `.firmament` files.

In v1 today, the implemented golden path is:

1. write a `.firmament` source file using the canonical indentation-based syntax
2. compile it through parse, validation, lowering, execution, and validation-execution stages
3. export the selected executed body to STEP AP242 text

## What Firmament currently supports

Firmament currently supports these source-level concepts:

- primitive features: `box`, `cylinder`, `sphere`
- boolean features: `add`, `subtract`, `intersect`
- validation ops: `expect_exists`, `expect_selectable`, `expect_manifold`
- placement blocks with `place.on` and `offset[3]`
- selector-shaped references rooted at a source feature id
- optional schema blocks for `cnc`, `additive`, and `injection_molded`
- first schema-aware CNC DFM validation for `minimum_tool_radius` on subtract-with-cylinder inputs
- canonical formatting of supported `.firmament` source
- STEP AP242 export for the current single-body golden path

## Core concepts

### Features

Features are ordered ops in `ops[n]`.

- primitive ops create geometry
- boolean ops consume earlier geometry and may create later geometry
- validation ops check conditions and do not create export bodies

Source feature ids are the stable language identity.

### Selectors

Selectors start from a source feature id and currently use one port:

- `feature_id`
- `feature_id.port`

Selectors are contract-based. The allowed ports depend on the referenced feature kind.

### Validation ops

Validation ops check feature existence, selector selectability, and manifold expectations against the currently implemented execution contracts.

### Placement

Placement currently supports an anchor plus a translation offset:

- `place.on: origin`
- `place.on: feature_id.port`
- `offset[3]` numeric translation

This is intentionally narrow. Arbitrary rotations and generalized transform stacks are not part of the current language surface.

### Schema

Schema is optional metadata attached to the document.

- `cnc` supports `minimum_tool_radius`
- `additive` supports `printer_resolution`
- `injection_molded` supports `parting_plane`, `gate_location`, and `draft_angle`

Only the CNC `minimum_tool_radius` rule is currently enforced as schema-aware DFM behavior.

## What Firmament does not support yet

The current implementation does **not** claim support for:

- sketch/extrude authoring systems
- PMI authoring or PMI export
- assembly modeling or assembly export
- multi-body export
- selector chaining or multi-hop selector resolution
- topology-stable semantic naming beyond current contract/runtime checks
- arbitrary orientation systems or generalized transforms
- a broad manufacturing-intelligence or full DFM engine

For concrete examples, see `testdata/firmament/examples/`. For the practical golden path, see `docs/firmament-build-workflow.md`.
