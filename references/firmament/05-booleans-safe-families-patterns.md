# Booleans, Safe-Family State, and Pattern Expansion

This file documents executable boolean and pattern semantics as implemented.

## Boolean op contracts

Supported boolean ops:
- `add`: requires `id`, `to`, `with`
- `subtract`: requires `id`, `from`, `with`
- `intersect`: requires `id`, `left`, `with`

`with` must be a nested primitive tool object-like value.
Supported tool ops inside `with`:
- `box`, `cylinder`, `sphere`, `cone`, `torus`

---

## Boolean result selector ports

All boolean result features (`add`/`subtract`/`intersect`) expose the same selector contract:
- `top_face`
- `bottom_face`
- `side_faces`
- `edges`
- `vertices`

This is intentionally not a union of all primitive-tool ports.

---

## Boolean reference fields vs placement anchors

Do not conflate these:

- Reference field (`to`/`from`/`left`) chooses the primary boolean source feature.
- Placement anchor fields (`place.on`, `place.on_face`, etc.) choose anchor sampling for placement translation.

A boolean can subtract from one feature while anchoring to another selector source.

---

## Safe-family state machine (implemented boundary)

Execution tracks feature graph states per produced feature ID:
- `BoxRoot`
- `CylinderRoot`
- `BoundedOrthogonalAdditiveSafeRoot`
- `BoundedOrthogonalAdditiveOutsideSafeRoot`
- `SafeSubtractComposition`
- `Other`

### Core implications

1. Subtract continuation is not purely syntax-based.
2. A parsed/validated boolean can still fail feature-graph safety checks.
3. Re-entry from `Other` into safe subtract continuation is blocked.

### Orthogonal additive transition

`add` may classify to:
- `BoundedOrthogonalAdditiveSafeRoot` (recognized safe), or
- `BoundedOrthogonalAdditiveOutsideSafeRoot` (recognized outside safe subset).

Subtract continuation from these states is guarded and narrower than generic boolean parsing suggests.

### Supported continuation family

Current continuation family is intentionally narrow:
- follow-on op must be `subtract`
- follow-on tool kind must be `cylinder` or `cone`
- geometric guards enforce non-overlap/non-tangent/non-degenerate-safe constraints

Anything outside this family should be treated as deferred/unsupported unless explicitly test-backed.

---

## Placement timing inside booleans

Boolean placement has two execution modes:

1. **Tool-placement mode** (selected path): place translated tool before boolean.
2. **Result-placement mode** (fallback/general): run boolean first, then translate resulting body.

Pattern-generated booleans always place tool first in their dedicated execution branch.

This timing distinction is a major source of authoring mistakes.

---

## Pattern ops (P2 subset)

Supported:
- `pattern_linear`
- `pattern_circular`

Both require:
- `source` referencing an earlier feature ID
- source feature must be a boolean `subtract`

Pattern ops themselves are **not** feature-producing geometry.
They are compile-time expansion directives that inject synthesized boolean ops.

### `pattern_linear`

Required:
- `count` positive integer
- `step[3]` numeric vector

`step[3]` meaning:
- world-space translation delta added per instance
- instance `i` gets offset contribution `i * step`

Synthesized IDs:
- `source__lin1`, `source__lin2`, ... `source__linN`

Reference chaining:
- each generated boolean rebinds its primary reference (`from`/`to`/`left`) to the previous instance ID
- first generated instance references original `source`

So generated instances are sequential composition, not independent clones from root.

### `pattern_circular`

Required:
- `count` positive integer
- `axis` selector
- exactly one of `angle_degrees` (span) or `angle_step_degrees`

Expansion behavior:
- synthesized IDs: `source__cir1..source__cirN`
- cloned placement injects `around_axis = axis`
- `radial_offset` defaults to `0` if absent on source placement
- angle per instance uses either explicit step or `span / count`
- base source placement angle (if any) is included

Also chains primary boolean reference sequentially like linear pattern.

---

## When pattern expansion happens

Pattern expansion occurs during document coherence validation, before lowering/execution/export body selection.

After expansion, downstream stages operate on concrete generated boolean entries as if authored directly.
