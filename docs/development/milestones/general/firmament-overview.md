# Historical / Firmament V1 Compatibility Documentation

> This document describes the legacy Firmament V1 authoring/execution model retained for compatibility and regression coverage. Firmament V2 is the current canonical authoring language; see [the serialization authority](../../architecture/system/firmament-serialization-compatibility.md).

# Firmament Overview

Firmament now includes bounded static logic: PascalCase `Enum` declarations and exhaustive enum/boolean `Match` expressions are evaluated into typed Concept IR and erased before Feature AIR. This is compile-time selection, not runtime behavior or general scripting. See [CONCEPT-STATIC-LOGIC-M3](concept-static-logic-m3.md).

The current language Concept implementation is documented by [CONCEPT-EXPANSION-M1](concept-expansion-m1.md) and [CONCEPT-MATERIALIZATION-M2](concept-materialization-m2.md). M2 adds typed Concept `Point3` consumption by semantic holes and explicit `Expose`-based structural conformance for materialized `Struct`/`Model` declarations; neither path performs automatic BRep semantic inference.

The production PascalCase front end now includes compile-time `Concept` contracts, non-materialized `Concept Struct` spatial values, and interchangeable materialized `Struct` / `Model` aliases. See [CONCEPT-EXPANSION-M1](concept-expansion-m1.md) for syntax, Concept IR, static expansion, conformance, erasure, provenance, and the distinction from Forge C# concepts.

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
- placement blocks with a unified model: semantic-first `on_face` anchors plus explicit `on` + `offset[3]` fallback (`centered_on` remains a compatibility alias), and optional axis placement (`around_axis`, `radial_offset`, `angle_degrees`)
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

Placement currently supports one coherent model with two input styles:

- semantic anchor: `place.on_face: feature_id.port` (`place.centered_on` accepted as a compatibility alias and normalized to `on_face`)
- explicit spatial fallback: `place.on: origin|feature_id.port` with `offset[3]`
- optional radial placement: `place.around_axis: feature_id.port` (+ optional `place.radial_offset` and `place.angle_degrees`)

This is intentionally narrow. It lowers deterministically into selector anchor extraction + axis/radial translation and does not introduce a general frame system or constraint solver.

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

For concrete examples, see `fixtures/Compatibility/LegacyV1/Examples/`. For selector/semantic naming contracts, see `docs/development/milestones/general/firmament-selectors.md`. For executable placement rules, see `docs/development/milestones/general/firmament-placement-semantics.md`. For the practical golden path, see `docs/development/milestones/general/firmament-build-workflow.md`.
